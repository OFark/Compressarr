using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
        
    
    public class Episode
    {
        [JsonProperty("seriesId")]
        public int SeriesId { get; set; }

        [JsonProperty("episodeFileId")]
        public int EpisodeFileId { get; set; }

        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("episodeNumber")]
        public int EpisodeNumber { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("airDate")]
        public string AirDate { get; set; }

        [JsonProperty("airDateUtc")]
        public DateTime AirDateUtc { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("hasFile")]
        public bool HasFile { get; set; }

        [JsonProperty("monitored")]
        public bool Monitored { get; set; }

        [JsonProperty("unverifiedSceneNumbering")]
        public bool UnverifiedSceneNumbering { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("absoluteEpisodeNumber")]
        public int? AbsoluteEpisodeNumber { get; set; }
    }    

    public class ManualImportEpisodeResponse
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("relativePath")]
        public string RelativePath { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("size")]
        public object Size { get; set; }

        [JsonProperty("series")]
        public SeriesJSON Series { get; set; }

        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("episodes")]
        public List<Episode> Episodes { get; set; }

        [JsonProperty("quality")]
        public EpisodeFile.EpisodeQuality Quality { get; set; }

        [JsonProperty("language")]
        public Language Language { get; set; }

        [JsonProperty("qualityWeight")]
        public int QualityWeight { get; set; }

        [JsonProperty("rejections")]
        public List<Rejection> Rejections { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("folderName")]
        public string FolderName { get; set; }
    }



}
