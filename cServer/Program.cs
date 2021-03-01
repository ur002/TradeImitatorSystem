using Newtonsoft.Json;
using Server.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Client.Classes;

namespace cServer
{
    class Program
    {
        
        static readonly object _lock = new object();
        static readonly Dictionary<string, TcpClient> DClients = new Dictionary<string, TcpClient>();
        static TOrderBook _GlobalOrderBook = new TOrderBook();
        static TTradeHistory _TradeHIstory = new TTradeHistory();
        static bool _newclientDetected = false;
        static CancellationTokenSource _ctsUpdateToken;
        static CancellationTokenSource _ctsWorkToken;

        /// <summary>
        /// MainProgram 
        /// </summary>
        static void Main(string[] args)
        {
            int ClientsCount = 0;
            TcpListener ServerSocket = new TcpListener(IPAddress.Any, 15051);
            ServerSocket.Start();
            _GlobalOrderBook.Init();
            _TradeHIstory.Init();
            Logger.InitLogger();           
            Logger.Log.Info("Server started");
            _ctsUpdateToken = new CancellationTokenSource();
            _ctsWorkToken = new CancellationTokenSource();


            while (true)
            {
                TcpClient client = ServerSocket.AcceptTcpClient();
                string UsGuid = Guid.NewGuid().ToString();
                lock (_lock) DClients.Add(UsGuid, client);
                _newclientDetected = true;
                Console.WriteLine($"New client {client.Client.RemoteEndPoint} connected!!");
                Logger.Log.Info($"New client {client.Client.RemoteEndPoint} connected!!");
                var CheckClient = Task.Run(() => IsConnected(client), _ctsUpdateToken.Token);
                var HandleClient = Task.Run(() => HandleClients(UsGuid), _ctsWorkToken.Token);                
                ClientsCount++;
            }

        }

        /// <summary>
        /// Checks the connection state
        /// </summary>
        /// <returns>True on connected. False on disconnected.</returns>
        static bool IsConnected(TcpClient c)
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

        public static void CheckClientAlive()
        {
            while (true)
            {
                try
                {
                    foreach (var c in DClients.Values)
                    {
                        try
                        {
                            if (IsConnected(c) == false)
                            {
                                var cluid = DClients.FirstOrDefault(x => x.Value == c).Key;
                                {
                                    if (cluid != null)
                                    {
                                        lock (_lock) DClients.Remove(cluid);
                                        Console.WriteLine($"Client {c.Client.RemoteEndPoint} was disconnected!!");
                                        c.Client.Shutdown(SocketShutdown.Both);
                                        c.Close();
                                    }
                                    else
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorCatcher(ex, "CheckClientAlive2");
                        }

                    }
                }
                catch (Exception ex)
                {

                    ErrorCatcher(ex, "CheckClientAlive1"); 
                }

                Thread.Sleep(50);
            }

        }

        /// <summary>
        /// Handle with clients
        /// </summary>
        /// <param name="cluid">ClientUID</param>
        public static void HandleClients(string cluid)
        {
            try
            {
                string clientid = cluid;
                TcpClient Client;
                lock (_lock) Client = DClients[clientid];

                if (_newclientDetected)
                {
                    byte[] buffer = Encoding.ASCII.GetBytes($"/CLIENTNUMBER;{clientid}");
                    Sendbuf2Client (Client , buffer);                  
                }
                while (true)
                {
                    NetworkStream stream = Client.GetStream();
                    byte[] buffer = new byte[2048];
                    int byte_count = stream.Read(buffer, 0, buffer.Length);
                    if (byte_count == 0) break;
                    
                    readCommand(Client, clientid, buffer, byte_count);                   
                }
                Console.WriteLine($"Client {Client.Client.RemoteEndPoint} was disconnected!!");
                Logger.Log.Info($"Client {Client.Client.RemoteEndPoint} was disconnected!!");
                lock (_lock) DClients.Remove(clientid);              
                Client.Client.Shutdown(SocketShutdown.Both);
                Client.Close();
            }
            catch(Exception ex)
            {
                ErrorCatcher(ex, "HandleClients");
            }
        }

        /// <summary>
        /// Read Command from Client
        /// </summary>
        /// <param name="client">TCPCLIENT</param>
        /// <param name="clientid">ClientID.</param>
        /// <param name="sCommand">received command text.</param>
        /// <param name="byte_count">received length of sCommand.</param>
        public static void readCommand(TcpClient client,string clientid,byte[] sCommand, int byte_count)
        {
            string exception4send = "";
                string ReadData = Encoding.ASCII.GetString(sCommand, 0, byte_count);
                Console.WriteLine(client.Client.RemoteEndPoint + ":" + ReadData);
                switch (ReadData.Split(';').First())
                {
                    case "/UPDATELASTGUIDORDERS":
                        string guid4update = ReadData.Split(';')[1].ToString();
                        _GlobalOrderBook.UpdateLastGuid(guid4update, clientid);
                        _TradeHIstory.UpdateLastGuid(guid4update, clientid);
                        break;
                    case "/GETORDERBOOK":
                        SendOrderBooktoClients(_GlobalOrderBook, client);
                        SendTradeHistorytoClients(_TradeHIstory);
                        break;
                    case "/ADDNEWORDER":
                        string OrderStr = ReadData.Split(';')[1].ToString();
                        if (OrderStr.Length > 0)
                        {
                            var O = TOrder.Deserialize(OrderStr);
                            bool TradeDone = CheckOrderForTrade(O);
                            if (!TradeDone)
                                _GlobalOrderBook.AddNewOrder(O);
                            else
                                SendTradeHistorytoClients(_TradeHIstory);
                            SendOrderBooktoClients(_GlobalOrderBook);
                        }
                            break;
                    case "/REMOVEORDER":
                        SendOrderBooktoClients(_GlobalOrderBook);
                        break;
                }
           if (exception4send.Length>0)
            {
                Sendbuf2Client(client, Encoding.ASCII.GetBytes("/ERROR;" + exception4send)); 
            }
        }

        /// <summary>
        /// Send byte[] buffer to TCPCLIENT
        /// </summary>
        /// <param name="c">TCPCLIENT for send.</param>
        /// <param name="buffer">byte[] buffer for send.</param>
        public static void Sendbuf2Client(TcpClient c,byte[] buffer)
        {
            try
            {
                if (IsConnected(c))
                {
                    NetworkStream stream = c.GetStream();
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            catch(Exception ex)
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
                    var datasend = JsonConvert.SerializeObject(_GlobalOrderBook);
                    byte[] buffer = Encoding.ASCII.GetBytes("/ORDERBOOK;" + datasend);
                    lock (_lock)
                    {
                        if (client != null)
                        {
                            Sendbuf2Client(client, buffer);
                        }
                        else
                            foreach (TcpClient c in DClients.Values)
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
                            foreach (TcpClient c in DClients.Values)
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
                foreach (TcpClient c in DClients.Values)
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
                var OBook = _GlobalOrderBook.Orders.Where(w => ( w.Side != O.Side  && w.Symbol == O.Symbol && (O.Side == TOrderSide.Sell? w.Price >= O.Price: w.Price <= O.Price)));
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
                                _TradeHIstory.MakeTrade(NewTrade);
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
                                _TradeHIstory.MakeTrade(NewTrade);
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
                        _GlobalOrderBook.AddNewOrder(O);
                    }
                    foreach (var OD in Orders4Del)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"Close Order from {OD.UserID}  side/symbol/qty/price  {OD.Side}/{OD.Symbol}/{OD.Quantity}/{O.Price}");
                        Logger.Log.Info($"Close Order from {OD.UserID}  side/symbol/qty/price  {OD.Side}/{OD.Symbol}/{OD.Quantity}/{O.Price}");
                        Console.ForegroundColor = ConsoleColor.White;
                        _GlobalOrderBook.Orders.Remove(OD);
                    }

                }

            }
            catch(Exception ex)
            {
                ErrorCatcher(ex, "CheckOrderForTrade");
            }
            return res;

        }

        /// <summary>
        /// Catch an error
        /// </summary>
        /// <param name="ex"></param>
        public static void ErrorCatcher(Exception ex, string eventname="")
        {
            DateTime dt = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + dt.ToString("yyyy-MM-dd") + "]");
            Console.WriteLine("* System: Error has occurred: " + (eventname.Length>0?"@"+ eventname+" ":"")+ ex.HResult + " " + ex.Message + Environment.NewLine + "* System: " + ex.StackTrace);
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
