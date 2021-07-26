using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
       

    public class ImportEpisodePayload
    {
        public string importMode = "auto";

        public string name = "ManualImport";

        [JsonProperty("files")]
        public List<File> Files { get; set; }
        
        public class File
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("seriesId")]
            public int SeriesId { get; set; }

            [JsonProperty("episodeIds")]
            public List<int> EpisodeIds { get; set; }

            [JsonProperty("quality")]
            public EpisodeFile.EpisodeQuality Quality { get; set; }

            [JsonProperty("language")]
            public Language Language { get; set; }
        }
    }

}
