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

namespace cServer
{
    class Program
    {
        
        static readonly object _lock = new object();
        static readonly Dictionary<string, TcpClient> DClients = new Dictionary<string, TcpClient>();
        static TOrderBook _GlobalOrderBook = new TOrderBook();
        static TTradeHistory _TradeHIstory = new TTradeHistory();
        static bool _newclientDetected = false;
        static bool _programclosing = false;

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
            ///Thread tchkdelc = new Thread(() => CheckClientAlive());
            //tchkdelc.Start();
            while (true)
            {
                TcpClient client = ServerSocket.AcceptTcpClient();
                string UsGuid = Guid.NewGuid().ToString();
                lock (_lock) DClients.Add(UsGuid, client);
                _newclientDetected = true;
                Console.WriteLine($"New client {client.Client.RemoteEndPoint} connected!!");
                Thread t = new Thread(() => HandleClients(UsGuid));
                t.Start();              
                ClientsCount++;
            }
            _programclosing = true;

        }

        static void CheckClientAlive()
        {
            while (_programclosing)
            {
                try
                {
                    foreach (var c in DClients.Values)
                    {
                        try
                        {
                            if (c.Client.Connected == false)
                            {
                                var cluid = DClients.FirstOrDefault(x => x.Value == c).Key;
                                lock (_lock) DClients.Remove(cluid);
                                Console.WriteLine($"Client {c.Client.RemoteEndPoint} was disconnected!!");
                                c.Client.Shutdown(SocketShutdown.Both);
                                c.Close();
                            }
                        }
                        catch (Exception ex)
                        {

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Exception was acquired@CheckClientAlive2: {ex.Message }.");
                            Console.ForegroundColor = ConsoleColor.White;
                        }

                    }
                }
                catch (Exception ex)
                {

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception was acquired@CheckClientAlive1: {ex.Message }.");
                    Console.ForegroundColor = ConsoleColor.White;
                }

                Thread.Sleep(50);
            }

        }
        /// <summary>
        /// Handle with clients
        /// </summary>
        private  static void HandleClients(string cluid)
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

                lock (_lock) DClients.Remove(clientid);
                Console.WriteLine($"Client {Client.Client.RemoteEndPoint} was disconnected!!");
                Client.Client.Shutdown(SocketShutdown.Both);
                Client.Close();
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red ;
                Console.WriteLine($"Exception was acquired@HandleClients  {ex.Message }.");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }


        /// <summary>
        /// Read Command from Client
        /// </summary>
        static void readCommand(TcpClient client,string clientid,byte[] sCommand, int byte_count)
        {
            string ReadData = Encoding.ASCII.GetString(sCommand, 0, byte_count);
            Console.WriteLine(client.Client.RemoteEndPoint + ":" + ReadData);

            switch (ReadData.Split(';').First())
            {
                case "/UPDATELASTGUIDORDERS":
                    string guid4update = ReadData.Split(';')[1].ToString();
                    _GlobalOrderBook.UpdateLastGuid(guid4update, clientid);
                    break;
                case "/GETORDERBOOK":
                    SendOrderBooktoClients(_GlobalOrderBook, client);
                    break;
                case "/ADDNEWORDER":                  
                    string OrderStr = ReadData.Split(';')[1].ToString() ;
                    var O = TOrder.Deserialize(OrderStr);
                    _GlobalOrderBook.AddNewOrder(O);
                    bool TradeDone = CheckOrderForTrade(O);
                    SendOrderBooktoClients(_GlobalOrderBook);
                    if (TradeDone )
                        SendTradeHistorytoClients(_TradeHIstory);
                    break;
                case "/REMOVEORDER":
                    SendOrderBooktoClients(_GlobalOrderBook);
                    break;
            }
        }

        /// <summary>
        /// Send byte[] buffer to TCPCLIENT
        /// </summary>
        /// <param name="c">TCPCLIENT for send.</param>
        /// <param name="buffer">byte[] buffer for send.</param>
        static void Sendbuf2Client(TcpClient c,byte[] buffer)
        {
            try
            {
                NetworkStream stream = c.GetStream();
                stream.Write(buffer, 0, buffer.Length);
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Exception was acquired@Sendbuf2Client. {ex.Message }.{ex.StackTrace}");
                Console.ForegroundColor = ConsoleColor.White;
            }

        }
        
        /// <summary>
        /// Send Order Book to clients
        /// </summary>
        /// <param name="OrderBook">OrderBook for send.</param>
        private static void SendOrderBooktoClients(TOrderBook OrderBook, TcpClient client = null)
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
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception was acquired@SendOrderBooktoClients. {ex.Message }.{ex.StackTrace}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        /// <summary>
        /// Send TrdeHistory Book to clients
        /// </summary>
        /// <param name="TradeBook">TradeHistoryBook for send.</param>
        private static void SendTradeHistorytoClients(TTradeHistory TradeBook, TcpClient client = null)
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
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception was acquired@SendOrderBooktoClients. {ex.Message }.{ex.StackTrace}");
                    Console.ForegroundColor = ConsoleColor.White;
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
            //    if(O.Side == TOrderSide.Sell)
            bool res = false;
            {
                List<TOrder> Orders4Del = new List<TOrder>();
                int qty = O.Quantity;
                int i = 0;
                // GetOrders(UserID,Qty) from TradeBook with price<=O.Price and torderside.Ask orderby by addid

                var BookOnlySell = _GlobalOrderBook.Orders.Where(w => ( w.Side != O.Side  && w.UserID != O.UserID && w.Symbol == O.Symbol && (O.Side == TOrderSide.Sell? w.Price >= O.Price: w.Price <= O.Price)));
                if (BookOnlySell.Any())
                {
                    foreach (var OiB in BookOnlySell)
                    {
                        while (qty > 0)
                        {
                            if (qty >= OiB.Quantity)
                            {
                                TTrade NewTrade = new TTrade();
                                NewTrade.BuyUserID = O.UserID;
                                NewTrade.SellUserID = OiB.UserID;
                                NewTrade.Symbol = O.Symbol;
                                NewTrade.TradedPrice = OiB.Price;
                                NewTrade.TradedQuantity = OiB.Quantity;
                                _TradeHIstory.TradesList.Add(NewTrade);
                                
                                qty -= OiB.Quantity;
                                Orders4Del.Add(OiB);
                                res = true;
                                break;
                            }
                            else
                            {
                                TTrade NewTrade = new TTrade();
                                NewTrade.BuyUserID = O.UserID;
                                NewTrade.SellUserID = OiB.UserID;
                                NewTrade.Symbol = O.Symbol;
                                NewTrade.TradedPrice = OiB.Price;
                                NewTrade.TradedQuantity = qty;
                                _TradeHIstory.TradesList.Add(NewTrade);
                                OiB.Quantity -= qty;
                                qty = 0;
                                res = true;
                                break;
                            }




                            //     {
                            //    Tradebook.Order.CLose(); // Delete from TradeOrderBook/Mark inactive move Order to tradehistory
                            //       }
                        }
                    }

                        foreach(var OD in Orders4Del)
                            _GlobalOrderBook.Orders.Remove(OD);
                }

            }
            return res;

        }



    }




    
}
