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

    public class TOrderBook
    {
        [JsonProperty]
        public LinkedList<TOrder> Orders { get; set; }
        private LinkedListNode<TOrder> LastInsertedOrder { get; set; }
        public void Init()
        {
            Orders = new LinkedList<TOrder>();
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
        public int TradedQuantity { get; set; }
        [JsonProperty]
        public double TradedPrice { get; set; }

    }

    public class TTradeHistory
    {
        [JsonProperty]
        public List<TTrade> TradesList { get; set; }
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
