using TTRadeModel.DataModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class fClient : Form
    {

        static string _CapStr = "";
        static bool _ActiveConnection = false;
        static TcpClient _tcpclient;
        static string _UserID = string.Empty;
        static bool _DoUpdateOB = false;
        static string _LastUserID = string.Empty;
        static NetworkStream _ns;             
        static bool _DouUPdateTH = false;
        static string _CurrSymb = "";
        static TOrderBook _GlobalOrderBook = new TOrderBook();
        static TTradeHistory _TradeHistory = new TTradeHistory();
        static CancellationTokenSource _ctsUpdateToken;
        static CancellationTokenSource _ctsWorkToken;
        static int _LastOrderCNTInOB = 0;
        static int _LastTradeCNTInTRH = 0;


        public fClient()
        {
            InitializeComponent();
        }

        private void cmbBIDASK_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnTop.BackColor = cmbBIDASK.SelectedIndex == 1 ? Color.LightGreen : Color.MistyRose;
        }

        private void bReconnect_Click(object sender, EventArgs e)
        {

        }

        private void chReconnect_CheckedChanged(object sender, EventArgs e)
        {
            pnconnection.Visible = chReconnect.Checked;
        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            if (!_ActiveConnection) 
            {
                _ctsUpdateToken = new CancellationTokenSource();
                _ctsWorkToken = new CancellationTokenSource();
                MakeConn(txtHost.Text, Convert.ToInt32(txtPort.Text)); 
            }
            else
            {
                _ctsUpdateToken.Cancel();
                _ctsWorkToken.Cancel();
                Disconnect(_tcpclient);
                dgvMyOrders.DataSource = null;
                dgvOB.DataSource = null;
                dgvTradeHistory.DataSource = null;
            }

            pnconnection.Visible = !_ActiveConnection;
            pnOrder.Visible = _ActiveConnection;
            txtHost.Enabled = !_ActiveConnection;
            txtPort.Enabled = !_ActiveConnection;
            bConnect.Text = !_ActiveConnection ? "Connect" : "Disconnect";
            Text = _CapStr;

        }


        private void MakeConn(string sip, int port)
        {
            IPAddress ip = IPAddress.Parse(sip);
            try
            {
                _tcpclient = new TcpClient();
                _tcpclient.Connect(ip, port);
               
                _CapStr = $"Connected to {ip}:{port}";
                _ActiveConnection = true;
                _ns = _tcpclient.GetStream();
                if(_LastUserID.Length>0)  TMainModel.SetLastUserID(_LastUserID);

                var ReadTask = Task.Run(() => ReceiveData(_tcpclient, _ctsUpdateToken.Token), _ctsUpdateToken.Token);
                var UPdateDataTask = Task.Run(() => UPDateData(_ctsUpdateToken.Token), _ctsUpdateToken.Token);
               
            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode == 10061)
                {
                    MessageBox.Show($"Cannt connect to target machine, conenction was  refused", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    _CapStr = "Network Error: Can not connect to target machine";
                    _ActiveConnection = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                _ActiveConnection = false;
            }

        }



        private bool isConnected(TcpClient c)
        {
            bool res = true;
            try
            {
               
                if (c.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (c.Client.Receive(buff, SocketFlags.Peek) == 0)
                    {
                        // Client disconnected
                        res = false;
                    }

                }
            }catch(Exception ex)
            {
                return false;
            }


            return res;
        }
        private void Disconnect(TcpClient client)
        {
            if (client == null) return;
            if (isConnected(client))
            {
                client.Client.Shutdown(SocketShutdown.Send);
                _ns.Close();
                client.Close();
                client.Dispose();
                _CapStr = "disconnect from server!!";
                _ActiveConnection = false;
                Thread.Sleep(100);
            }

        }

        async void  ReceiveData(TcpClient client, CancellationToken cancellationToken)
        {
           
            try
            {
                NetworkStream nsw = client.GetStream();
                byte[] receivedBytes = new byte[2048];
                int byte_count;
             
                DateTime lastNWActivity = DateTime.Now;
                while (client.Connected && !cancellationToken.IsCancellationRequested)
                {
                    if (GetState(client) == TcpState.Established)
                    {
                        if ((byte_count = nsw.Read(receivedBytes, 0, receivedBytes.Length)) > 0)
                        {                        
                            TMainModel.readCommand(client,_UserID, receivedBytes, byte_count);
                        }
                        
                    }
                    
                }
                nsw.Close();


            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode == 10061)
                {
                    //MessageBox.Show($"Can not connect to target machine, conenction was  refused", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    _CapStr = "Network Error: Can not connect to target machine";

                    _ActiveConnection = false;
                    _tcpclient.Dispose();
                    _ActiveConnection = false;
                    _GlobalOrderBook.Clear();
                    _TradeHistory.Clear();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                if (!client.Connected)
                {
                    _CapStr = $"No connection";
                    _ActiveConnection = false;                    
                    _tcpclient.Dispose();
                    _GlobalOrderBook.Clear();
                    _TradeHistory.Clear();


                }
            }

        }



        private void fClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_ctsUpdateToken != null) _ctsUpdateToken.Cancel();
            if (_ctsWorkToken != null)  _ctsWorkToken.Cancel();
            Disconnect(_tcpclient);

        }

        public static TcpState GetState(TcpClient tcpClient)
        {
            var tcpstate = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .FirstOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint));
            return tcpstate != null ? tcpstate.State : TcpState.Unknown;
        }


        private void BMakeOrder_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32(txtQuantity.Text) == 0) { MessageBox.Show("Quantity must be qreater than 0"); return; }
            if (Convert.ToDouble(txtOrderPrice.Text.Replace('.', ',')) == 0) { MessageBox.Show("Price must be qreater than 0"); return; }

            TOrder NewOrder = new TOrder();
            NewOrder.UserID = _UserID;
            NewOrder.Side = (TOrderSide)cmbBIDASK.SelectedIndex;
            NewOrder.Symbol = cmbTradeSmbols.Text;
            NewOrder.Quantity = Convert.ToInt32(txtQuantity.Text);
            NewOrder.Price = Convert.ToDouble(txtOrderPrice.Text.Replace('.', ','));
            var dataString = JsonConvert.SerializeObject(NewOrder);

            TMainModel.Sendbuf2Client(_tcpclient, Encoding.ASCII.GetBytes(@"/ADDNEWORDER;" + dataString));

            txtQuantity.Text = "0";
            txtOrderPrice.Text = "0.0";
        }

        private void fClient_Load(object sender, EventArgs e)
        {
          
            if (File.Exists("_lastguid")) _LastUserID = File.ReadAllText("_lastguid");

            TMainModel.OBInit();
            TMainModel.TRHInit();

           
            TMainModel.SetOrderBook(_GlobalOrderBook);
            TMainModel.SetOrderTradeHistory(_TradeHistory);

            _GlobalOrderBook = TMainModel.GetOrderBook();
            _TradeHistory = TMainModel.GetTradeHistory();




            dgvOB.AutoGenerateColumns = false;
            dgvTradeHistory.AutoGenerateColumns = false;           
            dgvMyOrders.AutoGenerateColumns = false;

        }

        private async void UPDateData(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {

                   
                       Invoke((MethodInvoker)delegate
                        {
                            if (_UserID.Length == 0) _UserID = TMainModel.GetCurrentUserID();
                            this.Text = _ActiveConnection ? $"{_CapStr}({_UserID}) ob.CNT:{_GlobalOrderBook?.Orders?.Count}" : "noConnection";

                        });
                    
                  
                    _GlobalOrderBook = TMainModel.GetOrderBook();
                    _TradeHistory = TMainModel.GetTradeHistory();

                    if ( _LastOrderCNTInOB !=_GlobalOrderBook.Orders?.Count() && _GlobalOrderBook.Orders != null)
                    {
                    
                        try
                        {
                         var qmyorders = _GlobalOrderBook.Orders                           
                            .Where(w => (w.Symbol == _CurrSymb && w.UserID==_UserID))                            
                            .Select(cl => new
                            {
                                cl.Symbol,
                                cl.Side,
                                cl.Quantity,
                                cl.Price
                            }).ToList();

                            var grOB = _GlobalOrderBook.Orders
                            .OrderBy(o => o.Price)
                            .Where(w => w.Symbol == _CurrSymb)
                            .GroupBy(l => new { l.Side,  l.Price })
                            .Select(cl => new
                            {
                                Symbol = cl.First().Symbol,                                
                                MYBIDQTY = cl.First().Side == TOrderSide.Buy & cl.First().UserID == _UserID ? cl.Sum(s => s.Quantity) : 0,
                                MYASKQTY = cl.First().Side == TOrderSide.Sell & cl.First().UserID == _UserID ? cl.Sum(s => s.Quantity) : 0,
                                MKTBIDQTY = cl.First().Side == TOrderSide.Buy & cl.First().UserID != _UserID ? cl.Sum(s => s.Quantity) : 0,
                                MKTASKQTY = cl.First().Side == TOrderSide.Sell & cl.First().UserID != _UserID ? cl.Sum(s => s.Quantity) : 0,
                            Price = cl.First().Price
                            }).ToList();



                            if (InvokeRequired)
                            {
                                Invoke((MethodInvoker)delegate
                                {
                                    dgvOB.DataSource = grOB;
                                    dgvOB.Update();
                                    dgvMyOrders.DataSource = qmyorders;
                                    dgvMyOrders.Update();
                                });
                            }
                            else
                            {
                                dgvOB.DataSource = grOB;
                                dgvOB.Update();
                                dgvMyOrders.DataSource = qmyorders;
                                dgvMyOrders.Update();
                            }

                            _LastOrderCNTInOB = _GlobalOrderBook.Orders.Count();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"UPDateData.dtMyActiveOrders.{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }

                    if (_LastTradeCNTInTRH!= _TradeHistory.TradesList?.Count() && _TradeHistory.TradesList != null)
                    {
                        
                        try
                        {
                            if (InvokeRequired)
                            {
                                Invoke((MethodInvoker)delegate
                                {
                                    dgvTradeHistory.DataSource = _TradeHistory.TradesList.ToList();
                                    dgvTradeHistory.Update();
                                });
                            }
                            else
                            {
                                dgvTradeHistory.DataSource = _TradeHistory.TradesList.ToList();
                                dgvTradeHistory.Update();
                            }
                            _LastTradeCNTInTRH = _TradeHistory.TradesList.Count();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"UPDateData.dtTradeHistory.{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }                       
                    }                 


                }
                catch (Exception ex)
                { MessageBox.Show($"UPDateData.{ex.Message }", "UPDateData", MessageBoxButtons.OK, MessageBoxIcon.Exclamation); }
                Thread.Sleep(100);
            }
        }

        private void cmbTradeSmbols_SelectedIndexChanged(object sender, EventArgs e)
        {
            _DoUpdateOB = true;
            _CurrSymb = cmbTradeSmbols.Text;
        }

       
    }
}

