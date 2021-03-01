using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using TTRadeModel.Classes;

namespace TTRadeModel.DataModel
{
    public class TMainModel
    {
        private static TOrderBook _OB;
        private static TTradeHistory _TRH;
        static readonly object _lock = new object();
        static Dictionary<string, TcpClient> _DClients;
        static string _LastUserID = "";
        static string _CurrentUserID = "";

        public static void OBInit()
        {
            _OB = new TOrderBook();
            _OB.Init();
        }

        public static void TRHInit()
        {
            _TRH = new TTradeHistory();
            _TRH.Init();
        }

        public static void SetOrderBook(TOrderBook OB)
        {
            _OB = OB;
        }

        public static void SetLastUserID(string LastUserID)
        {
            _LastUserID = LastUserID;
        }

        public static string GetLastUserID()
        {            
            return _LastUserID ;
        }

        public static string GetCurrentUserID()
        {
            return _CurrentUserID;
        }

        public static Dictionary<string, TcpClient> DClientsInit()
        {
            _DClients = new Dictionary<string, TcpClient>();
            return _DClients;
        }
        public static void SetCurrentUserID(string CurrentUserID)
        {
            _CurrentUserID= CurrentUserID;
        }

        public static void SetDClients(Dictionary<string, TcpClient> DClients)
        {
            _DClients = DClients;
        }

        public static Dictionary<string, TcpClient> GetDClients()
        {
            return _DClients;
        }

        public static void AddCllientToDict(string usguid,TcpClient c)
        {
            lock (_lock) _DClients.Add(usguid, c);            
        }
        public static void RemoveClientFromDict( string usguid)
        {
            lock (_lock) _DClients.Remove(usguid);
        }

        public static TOrderBook GetOrderBook()
        {
            return _OB;
        }

        public static void SetOrderTradeHistory(TTradeHistory TRH)
        {
            _TRH = TRH;
        }

        public static TTradeHistory GetTradeHistory()
        {
            return _TRH;
        }

        /// <summary>
        /// Read Command from Client
        /// </summary>
        /// <param name="client">TCPCLIENT</param>
        /// <param name="clientid">ClientID.</param>
        /// <param name="sCommand">received command text.</param>
        /// <param name="byte_count">received length of sCommand.</param>
        public static void readCommand(TcpClient client, string clientid, byte[] sCommand, int byte_count)
        {
            string exception4send = "";
            string ReadData = Encoding.ASCII.GetString(sCommand, 0, byte_count);
            Console.WriteLine(client.Client.RemoteEndPoint + ":" + ReadData);
            switch (ReadData.Split(';').First())
            {
                case "/UPDATELASTGUIDORDERS":
                    string guid4update = ReadData.Split(';')[1].ToString();
                    _OB.UpdateLastGuid(guid4update, clientid);
                    _TRH.UpdateLastGuid(guid4update, clientid);
                    break;
                case "/GETORDERBOOK":
                    SendOrderBooktoClients(_OB, client);
                    SendTradeHistorytoClients(_TRH);
                    break;
                case "/ADDNEWORDER":
                    string OrderStr = ReadData.Split(';')[1].ToString();
                    if (OrderStr.Length > 0)
                    {
                        var O = TOrder.Deserialize(OrderStr);
                        bool TradeDone = CheckOrderForTrade(O);
                        if (!TradeDone)
                            _OB.AddNewOrder(O);
                        else
                            SendTradeHistorytoClients(_TRH);
                        SendOrderBooktoClients(_OB);
                    }
                    break;
                case "/REMOVEORDER":
                    SendOrderBooktoClients(_OB);
                    break;
                case "/CLIENTNUMBER":
                    clientid = ReadData.Split(';')[1]?.ToString();
                    SetCurrentUserID(clientid);
                    if (_LastUserID.Length > 0) Sendbuf2Client(client, Encoding.ASCII.GetBytes("/UPDATELASTGUIDORDERS;" + _LastUserID));
                    File.WriteAllText("_lastguid", clientid);
                    Sendbuf2Client(client, Encoding.ASCII.GetBytes("/GETORDERBOOK;"));
                    break;
                case "/ORDERBOOK":
                    string jsdataOB = ReadData.Split(';')[1]?.ToString();
                    _OB = JsonConvert.DeserializeObject<TOrderBook>(jsdataOB);
                    //_DoUpdateOB = true;
                    // txtlog.Text += datasend.ToString() + Environment.NewLine;
                    break;
                case "/TRADEHISTORYBOOK":
                    string jsdataTH = ReadData.Split(';')[1]?.ToString();
                    _TRH = JsonConvert.DeserializeObject<TTradeHistory>(jsdataTH);
                    //_DouUPdateTH = true;
                    break;
            }
            if (exception4send.Length > 0)
            {
                Sendbuf2Client(client, Encoding.ASCII.GetBytes("/ERROR;" + exception4send));
            }
        }

        /// <summary>
        /// Send byte[] buffer to TCPCLIENT
        /// </summary>
        /// <param name="c">TCPCLIENT for send.</param>
        /// <param name="buffer">byte[] buffer for send.</param>
        public static void Sendbuf2Client(TcpClient c, byte[] buffer)
        {
            try
            {
                //if (IsConnected(c))
                {
                    NetworkStream stream = c.GetStream();
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                ErrorCatcher(ex, "Sendbuf2Client");
            }

        }

        /// <summary>
        /// Send Order Book to clients
        /// </summary>
        /// <param name="OrderBook">OrderBook for send.</param>
        public static void SendOrderBooktoClients(TOrderBook OrderBook, TcpClient client = null)
        {
            if (OrderBook != null && OrderBook.Orders.Count > 0)
            {
                try
                {
                    var datasend = JsonConvert.SerializeObject(_OB);
                    byte[] buffer = Encoding.ASCII.GetBytes("/ORDERBOOK;" + datasend);
                    lock (_lock)
                    {
                        if (client != null)
                        {
                            Sendbuf2Client(client, buffer);
                        }
                        else
                            foreach (TcpClient c in _DClients.Values)
                            {
                                Sendbuf2Client(c, buffer);
                            }
                    }
                }
                catch (Exception ex)
                {
                    ErrorCatcher(ex, "SendOrderBooktoClients");
                }
            }
        }

        /// <summary>
        /// Send TrdeHistory Book to clients
        /// </summary>
        /// <param name="TradeBook">TradeHistoryBook for send.</param>
        public static void SendTradeHistorytoClients(TTradeHistory TradeBook, TcpClient client = null)
        {
            if (TradeBook != null && TradeBook.TradesList.Count > 0)
            {
                try
                {
                    var datasend = JsonConvert.SerializeObject(TradeBook);
                    byte[] buffer = Encoding.ASCII.GetBytes("/TRADEHISTORYBOOK;" + datasend);
                    lock (_lock)
                    {
                        if (client != null)
                        {
                            Sendbuf2Client(client, buffer);
                        }
                        else
                            foreach (TcpClient c in _DClients.Values)
                            {
                                Sendbuf2Client(c, buffer);
                            }
                    }
                }
                catch (Exception ex)
                {
                    ErrorCatcher(ex, "SendTradeHistorytoClients");
                }
            }
        }
        /// <summary>
        /// Send broadcast message to Connected TCPCLIENTs
        /// </summary>
        /// <param name="msg">message for send.</param>
        public static void Broadcast2Clients(string msg)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(msg + Environment.NewLine);
            lock (_lock)
            {
                foreach (TcpClient c in _DClients.Values)
                {
                    Sendbuf2Client(c, buffer);
                }
            }
        }

        /// <summary>
        /// Engine.Check Order for trade
        /// </summary>
        /// <param name="O">Check Order for trade.</param>
        public static bool CheckOrderForTrade(TOrder O)
        {
            bool res = false;
            try
            {
                List<TOrder> Orders4Del = new List<TOrder>();
                int qty = O.Quantity;
                var OBook = _OB.Orders.Where(w => (w.Side != O.Side && w.Symbol == O.Symbol && (O.Side == TOrderSide.Sell ? w.Price >= O.Price : w.Price <= O.Price)));
                if (OBook.Any())
                {
                    foreach (var OiB in OBook)
                    {
                        bool ONeMoreOrder = false;
                        while (qty > 0 && !ONeMoreOrder)
                        {
                            if (qty >= OiB.Quantity)
                            {
                                TTrade NewTrade = new TTrade();
                                NewTrade.BuyUserID = O.UserID;
                                NewTrade.SellUserID = OiB.UserID;
                                NewTrade.Symbol = O.Symbol;
                                NewTrade.Price = OiB.Price;
                                NewTrade.Quantity = OiB.Quantity;
                                _TRH.MakeTrade(NewTrade);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"{NewTrade.BuyUserID} buy lot from {NewTrade.SellUserID} - {OiB.Quantity}!!");
                                Console.ForegroundColor = ConsoleColor.White;
                                Logger.Log.Info($"{NewTrade.BuyUserID} buy lot from {NewTrade.SellUserID} - {OiB.Quantity}!!");
                                qty -= OiB.Quantity;
                                Orders4Del.Add(OiB);
                                res = true;
                                ONeMoreOrder = true;
                            }
                            else
                            {
                                TTrade NewTrade = new TTrade();
                                NewTrade.BuyUserID = O.UserID;
                                NewTrade.SellUserID = OiB.UserID;
                                NewTrade.Symbol = O.Symbol;
                                NewTrade.Price = OiB.Price;
                                NewTrade.Quantity = qty;
                                _TRH.MakeTrade(NewTrade);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"{NewTrade.BuyUserID} buy lot from {NewTrade.SellUserID} - {qty} !!");
                                Logger.Log.Info($"{NewTrade.BuyUserID} buy lot from {NewTrade.SellUserID} - {qty} !!");
                                Console.ForegroundColor = ConsoleColor.White;
                                OiB.Quantity -= qty;
                                qty = 0;
                                res = true;
                            }

                        }
                    }


                    if (qty > 0)
                    {
                        O.Quantity = qty;
                        _OB.AddNewOrder(O);
                    }
                    foreach (var OD in Orders4Del)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"Close Order from {OD.UserID}  side/symbol/qty/price  {OD.Side}/{OD.Symbol}/{OD.Quantity}/{O.Price}");
                        Logger.Log.Info($"Close Order from {OD.UserID}  side/symbol/qty/price  {OD.Side}/{OD.Symbol}/{OD.Quantity}/{O.Price}");
                        Console.ForegroundColor = ConsoleColor.White;
                        _OB.Orders.Remove(OD);
                    }

                }

            }
            catch (Exception ex)
            {
                ErrorCatcher(ex, "CheckOrderForTrade");
            }
            return res;

        }
        /// <summary>
        /// Checks the connection state
        /// </summary>
        /// <returns>True on connected. False on disconnected.</returns>
        public static bool IsConnected(TcpClient c)
        {
            Socket nSocket = c.Client;
            try
            {
                if (nSocket.Connected)
                {
                    if ((nSocket.Poll(0, SelectMode.SelectWrite)) && (!nSocket.Poll(0, SelectMode.SelectError)))
                    {
                        byte[] buffer = new byte[1];
                        if (nSocket.Receive(buffer, SocketFlags.Peek) == 0)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorCatcher(ex, "IsConnected");
                return false;
            }
        }

        public static TcpState GetState(TcpClient tcpClient)
        {
            var tcpstate = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .FirstOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint));
            return tcpstate != null ? tcpstate.State : TcpState.Unknown;
        }

        /// <summary>
        /// Catch an error
        /// </summary>
        /// <param name="ex"></param>
        public static void ErrorCatcher(Exception ex, string eventname = "")
        {
            DateTime dt = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + dt.ToString("yyyy-MM-dd") + "]");
            Console.WriteLine("* System: Error has occurred: " + (eventname.Length > 0 ? "@" + eventname + " " : "") + ex.HResult + " " + ex.Message + Environment.NewLine + "* System: " + ex.StackTrace);
            Console.ForegroundColor = ConsoleColor.White;

            Logger.Log.Error("* System: Error has occurred: " + ex.HResult + " " + ex.Message + Environment.NewLine +
                   "* System: Stack Trace: " + ex.StackTrace + Environment.NewLine +
                   "* System: Inner Exception: " + ex.InnerException + Environment.NewLine +
                   "* System: Source: " + ex.Source + Environment.NewLine +
                  "* System: Target Site: " + ex.TargetSite + Environment.NewLine +
                  "* System: Help Link: " + ex.HelpLink);

        }


    }
}
