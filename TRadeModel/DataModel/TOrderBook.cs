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
        private bool _changed = false;

        [JsonProperty]
        public LinkedList<TOrder> Orders { get; set; }
        private LinkedListNode<TOrder> LastInsertedOrder { get; set; }
        [JsonProperty]
        public List<TTick> TickList { get; set; }

        public bool GetTOBChangeState()
        {
            return _changed;
        }
        public void SetTOBChangeState(bool changed)
        {
            _changed = changed;
        }

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
                SetTOBChangeState(true);
            }
        }
        public void RemoveOrder(TOrder O)
        {
            Orders.Remove(O);
            SetTOBChangeState(true);
        }
        public void UpdateLastGuid(string oldGuid, string NewGuid)
        {
            foreach (TOrder O in Orders)
                if (O.UserID == oldGuid) O.UserID = NewGuid;

        }

    }

}
