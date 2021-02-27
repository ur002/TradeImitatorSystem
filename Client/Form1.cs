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
        static TcpClient _tcpclient = new TcpClient();     
        static bool _THCanReadData = true;
        static string _UserID = string.Empty;
        static bool _DoUpdateOB = false;
        static string _LastUserID = string.Empty;
        static NetworkStream _ns;
        static Thread _ReadThread;
        static DataTable dtOrderbook = new DataTable("OrderBook");
        static DataTable dtTradeHistory = new DataTable("TradeHistory");
        static DataTable dtMyActiveOrders = new DataTable("dtMyActiveOrders"); 
        static bool _doUpdateData = true;
        static bool _DouUPdateTH = false;

        static TOrderBook _GlobalOrderBook = new TOrderBook();
        static TTradeHistory _TradeHistory = new TTradeHistory();
        public fClient()
        {
            InitializeComponent();
        }

        private void cmbBIDASK_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnTop.BackColor = cmbBIDASK.SelectedIndex ==1 ? Color.LightGreen : Color.MistyRose;
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
            MakeConn(txtHost.Text, Convert.ToInt32(txtPort.Text));
            Text = _CapStr;
            pnconnection.Visible = !_ActiveConnection;
            pnOrder.Visible = _ActiveConnection;


        }

        void MakeConn(string sip, int port)
        {
            IPAddress ip = IPAddress.Parse(sip);
            
            try
            {
                if (_tcpclient == null || _tcpclient.Client ==null)
                    _tcpclient = new TcpClient();
                
                if (!_tcpclient.Connected  )
                {
                    _tcpclient.Connect(ip, port);
                    
                    {
                        _CapStr = $"Connected to {ip}:{port}";
                        _ActiveConnection = true;
                         _ns = _tcpclient.GetStream();
                         _ReadThread = new Thread(() => ReceiveData(_tcpclient));
                        _ReadThread.Start();
                    }
                }
            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode == 10061)
                {
                    MessageBox.Show($"Cannt connect to target machine, conenction was  refused", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    _CapStr = "Network Error: Can not connect to target machine";
                    _THCanReadData = false;
                    _tcpclient.Close();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                _THCanReadData = false;
                _tcpclient.Close();
            }
            

        }



        static void Disconnect(TcpClient client)
        {
            if (client.Client.Connected)
            {
                client.Client.Shutdown(SocketShutdown.Send);
                //_ReadThread.Join();
                _ns.Close();
                client.Close();
                _CapStr = "disconnect from server!!";
            }

        }

        void ReceiveData(TcpClient client)
        {
            string res = "";
            try
            {

                NetworkStream nsw = client.GetStream();
                byte[] receivedBytes = new byte[1024];
                int byte_count;
                bool timeout = false;
                DateTime lastNWActivity = DateTime.Now;
                while ((client.Connected && !timeout) || _THCanReadData)
                {
                    //if (client.Client.Poll(1, SelectMode.SelectRead) && !nsw.DataAvailable)
                    if (GetState(client) == TcpState.Established)
                    {
                        if ((byte_count = nsw.Read(receivedBytes, 0, receivedBytes.Length)) > 0)
                        {
                            //res = Encoding.ASCII.GetString(receivedBytes, 0, byte_count);
                            readCommand(client, receivedBytes, byte_count);
                            //UpdatetxtLog(res);
                        }
                    }
                    else
                    {
                        if (DateTime.Now > lastNWActivity.AddSeconds(60))
                            timeout = true;
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
                    _THCanReadData = false;
                    _tcpclient.Dispose();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                if (!client.Connected)
                {
                    _CapStr = $"No connection";
                    _ActiveConnection = false;
                    _THCanReadData = false;
                    _tcpclient.Dispose();

                }
            }
           
        }


        /// <summary>
        /// Read Command from Client
        /// </summary>
         void readCommand(TcpClient client, byte[] sCommand, int byte_count)
        {
            string ReadData = Encoding.ASCII.GetString(sCommand, 0, byte_count);
            Console.WriteLine(client.Client.RemoteEndPoint + ":" + ReadData);

            switch (ReadData.Split(';').First())
            {
                case "/CLIENTNUMBER":
                    _UserID = ReadData.Split(';')[1]?.ToString();
                    if (_LastUserID.Length>0) sendmsg(_tcpclient, "/UPDATELASTGUIDORDERS;"+_LastUserID);
                    File.WriteAllText("_lastguid", _UserID);
                    sendmsg(_tcpclient, "/GETORDERBOOK;");
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

        void repaintORderBook()
        {


        }

     

        private void fClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect(_tcpclient);
            _doUpdateData = false;
        }

        public static TcpState GetState(TcpClient tcpClient)
        {
            var tcpstate = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .FirstOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint));
            return tcpstate != null ? tcpstate.State : TcpState.Unknown;
        }

        void sendmsg(TcpClient c, string msg)
        {
            try
            {
                if (c == null) c = new TcpClient();
                if (!c.Client.Connected)
                {
                    MakeConn(txtHost.Text, Convert.ToInt32(txtPort.Text));
                    Text = _CapStr;
                    pnconnection.Visible = !_ActiveConnection;
                }
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
            if (Convert.ToInt32(txtQuantity.Text) == 0 ) { MessageBox.Show("Quantity must be qreater than 0");return; }
            if (Convert.ToDouble(txtOrderPrice.Text.Replace('.', ',')) == 0) { MessageBox.Show("Price must be qreater than 0"); return; }

            TOrder NewOrder = new TOrder();
            NewOrder.UserID = _UserID;
            NewOrder.Side = (TOrderSide)cmbBIDASK.SelectedIndex;
            NewOrder.Symbol = cmbTradeSmbols.Text;
            NewOrder.Quantity = Convert.ToInt32(txtQuantity.Text);
            NewOrder.Price = Convert.ToDouble(txtOrderPrice.Text.Replace('.', ','));
    

            var dataString = JsonConvert.SerializeObject(NewOrder);            
            
            sendmsg(_tcpclient, @"/ADDNEWORDER;"+dataString);
            txtQuantity.Text = "0";
            txtOrderPrice.Text = "0.0";
        }

        private void fClient_Load(object sender, EventArgs e)
        {           
            _GlobalOrderBook.Init();
            if (File.Exists("_lastguid")) _LastUserID = File.ReadAllText("_lastguid");
            Thread THUpdater = new Thread(() => UPDateData());
            THUpdater.Start();

            #region CreateDataColumns
            dtOrderbook.Columns.Add("SYMBOL", typeof(string));
            dtOrderbook.Columns.Add("MYBIDQTY", typeof(int));
            dtOrderbook.Columns.Add("MYASKQTY", typeof(int));
            dtOrderbook.Columns.Add("MKTBIDQTY", typeof(int));
            dtOrderbook.Columns.Add("MKTASKQTY", typeof(int));
            dtOrderbook.Columns.Add("PRICE", typeof(double));

            dtTradeHistory.Columns.Add("BuyUserID", typeof(string));
            dtTradeHistory.Columns.Add("SellUserID", typeof(string));
            dtTradeHistory.Columns.Add("Symbol", typeof(string));
            dtTradeHistory.Columns.Add("TradedPrice", typeof(double));
            dtTradeHistory.Columns.Add("TradedQuantity", typeof(int));

            dtMyActiveOrders.Columns.Add("SYMBOL", typeof(string));
            dtMyActiveOrders.Columns.Add("SIDE", typeof(string));
            dtMyActiveOrders.Columns.Add("QTY", typeof(int));
            dtMyActiveOrders.Columns.Add("PRICE", typeof(double));
            #endregion

            dgvOB.AutoGenerateColumns = false;
            dgvTradeHistory.AutoGenerateColumns = false;
            dgvOB.DataSource = dtOrderbook;
            dgvTradeHistory.DataSource = dtTradeHistory;
            dgvMyOrders.AutoGenerateColumns = false;
            dgvMyOrders.DataSource = dtMyActiveOrders;
        }

        private void UPDateData()
        {
            while (_doUpdateData)
            {

                if (InvokeRequired)
                {
                    Invoke((MethodInvoker)delegate
                    {
                       if( this.Text != _CapStr) this.Text = _CapStr;
                    });
                }
                else
                {
                    if (this.Text != _CapStr) this.Text = _CapStr;
                }
               
                if (_DoUpdateOB)
                {
                    try
                    {
                        dtOrderbook.Clear();
                        var subGroup = _GlobalOrderBook.Orders
                                .GroupBy(x => new { _Symbol = x.Symbol, _Side = x.Side, _UserID = x.UserID, _Price = x.Price });


                        foreach (TOrder O in _GlobalOrderBook.Orders)
                        {

                            {
                                int MYBIDQTY = O.Side == TOrderSide.Buy & O.UserID == _UserID ? O.Quantity : 0;
                                int MYASKQTY = O.Side == TOrderSide.Sell & O.UserID == _UserID ? O.Quantity : 0;
                                int MKTBIDQTY = O.Side == TOrderSide.Buy & O.UserID != _UserID ? O.Quantity : 0;
                                int MKTASKQTY = O.Side == TOrderSide.Sell & O.UserID != _UserID ? O.Quantity : 0;

                                dtOrderbook.Rows.Add(new object[] { O.Symbol, MYBIDQTY, MYASKQTY, MKTBIDQTY, MKTASKQTY, O.Price });
                            }
                            if(O.UserID == _UserID)
                            {
                                dtMyActiveOrders.Rows.Add(new object[] { O.Symbol, O.Side, O.Quantity, O.Price });
                            }
                        }



                        if (InvokeRequired)
                        {
                            Invoke((MethodInvoker)delegate
                            {                                                        
                                dgvOB.Update();
                                dgvMyOrders.Update();
                            });
                        }
                        else
                        {                                                    
                            dgvOB.Update();
                            dgvMyOrders.Update();
                        }


                        _DoUpdateOB = false;
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show($"UPDateData.{ex.Message }", "Network connection problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                   
                }

                if (_DouUPdateTH)
                {
                    dtTradeHistory.Clear();


                    foreach (TTrade Tr in _TradeHistory.TradesList)
                    {

                        dtTradeHistory.Rows.Add(new object[] { Tr.BuyUserID, Tr.SellUserID , Tr.Symbol, Tr.TradedQuantity, Tr.TradedPrice });
                    }

                    if (InvokeRequired)
                    {
                        Invoke((MethodInvoker)delegate
                        {                            
                            dgvTradeHistory.Update();
                        });
                    }
                    else
                    {                       
                        dgvTradeHistory.Update();
                    }

                    

                        _DouUPdateTH = false;
                }
                Thread.Sleep(100);
            }
           
        }
    }
}

