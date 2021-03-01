using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTRadeModel.DataModel
{

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
}
