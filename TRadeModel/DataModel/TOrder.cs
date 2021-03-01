using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTRadeModel.DataModel
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
