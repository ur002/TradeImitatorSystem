using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTRadeModel.DataModel
{
    public class TOrderBook
    {

        [JsonProperty]
        public LinkedList<TOrder> Orders { get; set; }
        private LinkedListNode<TOrder> LastInsertedOrder { get; set; }
        [JsonProperty]
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

    }

}
