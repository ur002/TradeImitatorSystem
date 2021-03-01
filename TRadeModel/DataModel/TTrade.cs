using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTRadeModel.DataModel
{


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
        public void MakeTrade(TTrade Trd)
        {
            if (TradesList == null) Init();
            TradesList.Add(Trd);
        }

        public void Clear()
        {
            if (TradesList == null) Init();
            TradesList.Clear();
        }

        public void UpdateLastGuid(string oldGuid, string NewGuid)
        {
            foreach (TTrade Tr in TradesList)
            {
                if (Tr.BuyUserID == oldGuid) Tr.BuyUserID = NewGuid;
                if (Tr.SellUserID == oldGuid) Tr.SellUserID = NewGuid;
            }

        }

    }
}
