using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    
    
    public class EpisodeMediaInfo
    {
        [Filter("Audio Channels", FilterPropertyType.Number)]
        [JsonProperty("audioChannels")]
        public double AudioChannels { get; set; }

        [Filter("Audio Codec", FilterPropertyType.Enum)]
        [JsonProperty("audioCodec")]
        public string AudioCodec { get; set; }

        [Filter("Video Codec", FilterPropertyType.Enum)]
        [JsonProperty("videoCodec")]
        public string VideoCodec { get; set; }
    }

    public class EpisodeFile : Media, IMedia
    {
        [JsonProperty("seriesId")]
        public int SeriesId { get; set; }

        [Filter("Season Number", FilterPropertyType.Number)]
        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("relativePath")]
        public string RelativePath { get; set; }

        [Filter("Path")]
        [JsonProperty("path")]
        public string Path { get; set; }

        [Filter("Size", FilterPropertyType.Number)]
        [JsonProperty("size")]
        public long Size { get; set; }

        [Filter("Date Added", FilterPropertyType.DateTime)]
        [JsonProperty("dateAdded")]
        public DateTime DateAdded { get; set; }

        [Filter("Scene Name")]
        [JsonProperty("sceneName")]
        public string SceneName { get; set; }

        [Filter("Quality", true)]
        [JsonProperty("quality")]
        public Quality Quality { get; set; }

        [JsonProperty("language")]
        public Language Language { get; set; }

        [Filter("Media Info", true)]
        [JsonProperty("mediaInfo")]
        public EpisodeMediaInfo MediaInfo { get; set; }

        [JsonProperty("originalFilePath")]
        public string OriginalFilePath { get; set; }

        [Filter("Quality cut off not met", FilterPropertyType.Boolean)]
        [JsonProperty("qualityCutoffNotMet")]
        public bool QualityCutoffNotMet { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonIgnore]
        public string FilePath => $"{BasePath}{Path}";

        public int GetStableHash()
        {
            return JsonConvert.SerializeObject(this).GetStableHashCode();
        }
    }


}
