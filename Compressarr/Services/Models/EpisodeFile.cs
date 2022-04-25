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

        [Filter("Size(F)", FilterPropertyType.FileSize, FilterOn = "Size")]
        public string SizeNice => Size.ToFileSize();

        [Filter("Date Added", FilterPropertyType.DateTime)]
        [JsonProperty("dateAdded")]
        public DateTime DateAdded { get; set; }

        [Filter("Scene Name")]
        [JsonProperty("sceneName")]
        public string SceneName { get; set; }

        [Filter("Quality", true)]
        public EpisodeQuality Quality { get; set; }

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

        [JsonIgnore]
        public new string FilePath => $"{BasePath}{Path}";

        public class FileQuality
        {
            public int Id { get; set; }

            [Filter("Modifier", FilterPropertyType.Enum)]
            [JsonProperty("modifier")]
            public string Modifier { get; set; }

            [Filter("Name", FilterPropertyType.Enum)]
            [JsonProperty("name")]
            public string Name { get; set; }

            [Filter("Resolution", FilterPropertyType.Enum)]
            [JsonProperty("resolution")]
            public string Resolution { get; set; }

            [Filter("Source", FilterPropertyType.Enum)]
            [JsonProperty("source")]
            public string Source { get; set; }
        }

        public class EpisodeQuality
        {
            [JsonProperty("customFormats")]
            public List<object> CustomFormats { get; set; }

            [Filter("File Quality", true)]
            [JsonProperty("quality")]
            public FileQuality FileQuality { get; set; }

            [Filter("Revision", true)]
            [JsonProperty("revision")]
            public Revision Revision { get; set; }
        }
    }


}
