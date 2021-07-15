using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    public class Rejection
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
