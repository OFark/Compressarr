using Compressarr.Filtering;
using Newtonsoft.Json;

namespace Compressarr.Services.Models
{
    public class Ratings
    {
        [Filter("Value", FilterPropertyType.Number)]
        [JsonProperty("value")]
        public double Value { get; set; }

        [Filter("Votes", FilterPropertyType.Number)]
        [JsonProperty("votes")]
        public int Votes { get; set; }
    }
}
