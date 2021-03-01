using Newtonsoft.Json;
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
using TTRadeModel;
using TTRadeModel.DataModel;
using TTRadeModel.Classes;

namespace cServer
{
    class Program
    {
        
        static readonly object _lock = new object();
        static Dictionary<string, TcpClient> _DClients;
        static TOrderBook _GlobalOrderBook = new TOrderBook();
        static TTradeHistory _TradeHistory = new TTradeHistory();
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
           
            Logger.InitLogger();           
            Logger.Log.Info("Server started");

            _ctsUpdateToken = new CancellationTokenSource();
            _ctsWorkToken = new CancellationTokenSource();

            _GlobalOrderBook.Init();
            _TradeHistory.Init();
            TMainModel.SetOrderBook(_GlobalOrderBook);
            TMainModel.SetOrderTradeHistory(_TradeHistory);
            _DClients = TMainModel.DClientsInit();
            

            while (true)
            {
                TcpClient client = ServerSocket.AcceptTcpClient();
                string UsGuid = Guid.NewGuid().ToString();
                TMainModel.AddCllientToDict(UsGuid, client);
                _newclientDetected = true;
                Console.WriteLine($"New client {client.Client.RemoteEndPoint} connected!!");
                Logger.Log.Info($"New client {client.Client.RemoteEndPoint} connected!!");
                var CheckClient = Task.Run(() => TMainModel.IsConnected(client), _ctsUpdateToken.Token);
                var HandleClient = Task.Run(() => HandleClients(UsGuid), _ctsWorkToken.Token);                
                ClientsCount++;
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
                lock (_lock) Client = _DClients[clientid];

                if (_newclientDetected)
                {
                    byte[] buffer = Encoding.ASCII.GetBytes($"/CLIENTNUMBER;{clientid}");
                    TMainModel.Sendbuf2Client(Client , buffer);                  
                }
                while (true)
                {
                    NetworkStream stream = Client.GetStream();
                    byte[] buffer = new byte[2048];
                    int byte_count = stream.Read(buffer, 0, buffer.Length);
                    if (byte_count == 0) break;

                    TMainModel.readCommand(Client, clientid, buffer, byte_count);                   
                }
                Console.WriteLine($"Client {Client.Client.RemoteEndPoint} was disconnected!!");
                Logger.Log.Info($"Client {Client.Client.RemoteEndPoint} was disconnected!!");
                TMainModel.RemoveClientFromDict(clientid);
                Client.Client.Shutdown(SocketShutdown.Both);
                Client.Close();
            }
            catch(Exception ex)
            {
                TMainModel.ErrorCatcher(ex, "HandleClients");
            }
        }

      



    }




    
}
