using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.DataModel
{
    public class TOrder
    {
        [JsonProperty]
        public string UserID { get; set; }
        [JsonProperty]
        public string Symbol { get; set; }
        [JsonProperty]
        public TOrderSide Side { get; set; }
        [JsonProperty]
        public int Quantity { get; set; }
        [JsonProperty]
        public double Price { get; set; }

        public static TOrder Deserialize(string JSON)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TOrder>(JSON);
        }


    }

    public class TTick
    {

        [JsonProperty]
        public readonly double Price;
        [JsonProperty]
        public readonly int Quantity;

        public TTick(double price, int quantity)
        {
            Price = price;
            Quantity = quantity;
        }

    }

    public class TOrderBook
    {
        [JsonProperty]
        public LinkedList<TOrder> Orders { get; set; }
        private LinkedListNode<TOrder> LastInsertedOrder { get; set; }

        public List<TTick> TickList { get; set; }
        public void Init()
        {
            Orders = new LinkedList<TOrder>();
            TickList = new List<TTick>();
        }

        public void Clear()
        {
            if (Orders != null) Init();
            Orders.Clear();
        }
        public void AddNewOrder(TOrder O)
        {
            if (Orders != null)
            {
                TickList.Add(new TTick(O.Price, O.Quantity));
                if (LastInsertedOrder == null)
                    LastInsertedOrder = Orders.AddFirst(O);
                else
                    LastInsertedOrder = Orders.AddAfter(LastInsertedOrder, O);

            }
        }
        public void RemoveOrder(TOrder O)
        {
            Orders.Remove(O);
        }
        public void UpdateLastGuid(string oldGuid, string NewGuid)
        {
            foreach (TOrder O in Orders)
                if (O.UserID == oldGuid) O.UserID = NewGuid;

        }


        // List of orders at each tick 

    }

    public class TTrade
    {
        [JsonProperty]
        public string BuyUserID { get; set; }
        [JsonProperty]
        public string SellUserID { get; set; }
        [JsonProperty]
        public string Symbol { get; set; }
        [JsonProperty]
        public int Quantity { get; set; }
        [JsonProperty]
        public double Price { get; set; }

    }

    public class TTradeHistory
    {
        [JsonProperty]
        public List<TTrade> TradesList { get; set; }
        public void Init()
        {
            TradesList = new List<TTrade>();
        }
    }


    public enum TOrderSide : byte
    {
        /// <summary>
        /// Sell
        /// </summary>
        Sell,
        /// <summary>
        /// Buy
        /// </summary>
        Buy

    }
}
