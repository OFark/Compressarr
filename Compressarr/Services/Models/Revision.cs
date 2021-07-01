using Compressarr.Filtering;
using Newtonsoft.Json;

namespace Compressarr.Services.Models
{
    public class Revision
    {
        [Filter("Is Repack", FilterPropertyType.Boolean)]
        [JsonProperty("isRepack")]
        public bool IsRepack { get; set; }

        [Filter("Real", FilterPropertyType.Number)]
        [JsonProperty("real")]
        public int Real { get; set; }

        [Filter("Version", FilterPropertyType.Number)]
        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
