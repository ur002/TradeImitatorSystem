using Client.DataModel;
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
                if (c.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (c.Client.Receive(buff, SocketFlags.Peek) == 0)
                    {
                    // Client disconnected
                    res = false;
                    }

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
            }

        }

        async void  ReceiveData(TcpClient client, CancellationToken cancellationToken)
        {
           
            try
            {
                NetworkStream nsw = client.GetStream();
                byte[] receivedBytes = new byte[4096];
                int byte_count;
             
                DateTime lastNWActivity = DateTime.Now;
                while (client.Connected && !cancellationToken.IsCancellationRequested)
                {
                    if (GetState(client) == TcpState.Established)
                    {
                        if ((byte_count = nsw.Read(receivedBytes, 0, receivedBytes.Length)) > 0)
                        {                        
                            readCommand(client, receivedBytes, byte_count);
                        }
                    }
                    
                }

                nsw.Close();
            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode == 10061)
                {
                    MessageBox.Show($"Can not connect to target machine, conenction was  refused", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    _CapStr = "Network Error: Can not connect to target machine";                
                    _tcpclient.Dispose();
                    _ActiveConnection = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                if (!client.Connected)
                {
                    _CapStr = $"No connection";
                    _ActiveConnection = false;                    
                    _tcpclient.Dispose();

                }
            }

        }


        /// <summary>
        /// Read Command from Client
        /// </summary>
        async void readCommand(TcpClient client, byte[] sCommand, int byte_count)
        {
            string ReadData = Encoding.ASCII.GetString(sCommand, 0, byte_count);
            switch (ReadData.Split(';').First())
            {
                case "/CLIENTNUMBER":
                    _UserID = ReadData.Split(';')[1]?.ToString();
                    if (_LastUserID.Length > 0) sendmsg2srv(client, "/UPDATELASTGUIDORDERS;" + _LastUserID);
                    File.WriteAllText("_lastguid", _UserID);
                    sendmsg2srv(client, "/GETORDERBOOK;");
                    break;
                case "/ORDERBOOK":
                    string jsdataOB = ReadData.Split(';')[1]?.ToString();
                    _GlobalOrderBook = JsonConvert.DeserializeObject<TOrderBook>(jsdataOB);
                    _DoUpdateOB = true;
                    // txtlog.Text += datasend.ToString() + Environment.NewLine;
                    break;
                case "/TRADEHISTORYBOOK":
                    string jsdataTH = ReadData.Split(';')[1]?.ToString();
                    _TradeHistory = JsonConvert.DeserializeObject<TTradeHistory>(jsdataTH);
                    _DouUPdateTH = true;
                    break;
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

        void sendmsg2srv(TcpClient c, string msg)
        {
            try
            {             
                NetworkStream nsw = c.GetStream();
                if (GetState(c) == TcpState.Established)
                {
                    byte[] buffer = Encoding.ASCII.GetBytes(msg);
                    NetworkStream stream = c.GetStream();
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
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

            sendmsg2srv(_tcpclient, @"/ADDNEWORDER;" + dataString);

            txtQuantity.Text = "0";
            txtOrderPrice.Text = "0.0";
        }

        private void fClient_Load(object sender, EventArgs e)
        {
            _GlobalOrderBook.Init();
            if (File.Exists("_lastguid")) _LastUserID = File.ReadAllText("_lastguid");

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
                  
                    if (_DoUpdateOB)
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
                            _DoUpdateOB = false;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"UPDateData.dtMyActiveOrders.{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }

                    if (_DouUPdateTH)
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
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"UPDateData.dtTradeHistory.{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                        _DouUPdateTH = false;
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

