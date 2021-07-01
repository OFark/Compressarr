using Newtonsoft.Json;

namespace Compressarr.Services.Models
{
    public class Language
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
