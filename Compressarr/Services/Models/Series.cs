using Compressarr.Filtering;
using Compressarr.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    public class AlternateTitle
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("sceneSeasonNumber")]
        public int? SceneSeasonNumber { get; set; }
    }

    public class Statistics
    {
        [Filter("Episode File Count", FilterPropertyType.Number)]
        [JsonProperty("episodeFileCount")]
        public int EpisodeFileCount { get; set; }

        [Filter("Episode Count", FilterPropertyType.Number)]
        [JsonProperty("episodeCount")]
        public int EpisodeCount { get; set; }

        [JsonProperty("totalEpisodeCount")]
        public int TotalEpisodeCount { get; set; }

        [Filter("Size on Disk", FilterPropertyType.Number)]
        [JsonProperty("sizeOnDisk")]
        public long SizeOnDisk { get; set; }

        [JsonProperty("percentOfEpisodes")]
        public double PercentOfEpisodes { get; set; }

        [JsonProperty("previousAiring")]
        public DateTime? PreviousAiring { get; set; }

        [JsonProperty("nextAiring")]
        public DateTime? NextAiring { get; set; }
    }

    public class Season
    {
        [Filter("Season Number", FilterPropertyType.Number)]
        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("monitored")]
        public bool Monitored { get; set; }

        [Filter("Statistics", true)]
        [JsonProperty("statistics")]
        public Statistics Statistics { get; set; }

        [Filter("Episode Files", true)]
        public IEnumerable<EpisodeFile> EpisodeFiles { get; set; } = new HashSet<EpisodeFile>();
    }

    public partial class Series
    {
        [Filter("Title")]
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("ended")]
        public bool Ended { get; set; }

        [JsonProperty("alternateTitles")]
        public List<AlternateTitle> AlternateTitles { get; set; }

        [JsonProperty("sortTitle")]
        public string SortTitle { get; set; }

        [Filter("Season Count", FilterPropertyType.Number)]
        [JsonProperty("seasonCount")]
        public int SeasonCount { get; set; }

        [JsonProperty("totalEpisodeCount")]
        public int TotalEpisodeCount { get; set; }

        [Filter("Episode Count", FilterPropertyType.Number)]
        [JsonProperty("episodeCount")]
        public int EpisodeCount { get; set; }

        [Filter("Episode File Count", FilterPropertyType.Number)]
        [JsonProperty("episodeFileCount")]
        public int EpisodeFileCount { get; set; }

        [Filter("Size on Disk", FilterPropertyType.Number)]
        [JsonProperty("sizeOnDisk")]
        public long SizeOnDisk { get; set; }

        [Filter("Status", FilterPropertyType.Enum)]
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("previousAiring")]
        public DateTime PreviousAiring { get; set; }

        [Filter("Network", FilterPropertyType.Enum)]
        [JsonProperty("network")]
        public string Network { get; set; }

        [JsonProperty("images")]
        public List<Image> Images { get; set; }

        [Filter("Season", true)]
        [JsonProperty("seasons")]
        public IEnumerable<Season> Seasons { get; set; }

        [Filter("Year", FilterPropertyType.Number)]
        [JsonProperty("year")]
        public int Year { get; set; }

        [Filter("Path")]
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("profileId")]
        public int ProfileId { get; set; }

        [JsonProperty("languageProfileId")]
        public int LanguageProfileId { get; set; }

        [JsonProperty("seasonFolder")]
        public bool SeasonFolder { get; set; }

        [JsonProperty("monitored")]
        public bool Monitored { get; set; }

        [JsonProperty("useSceneNumbering")]
        public bool UseSceneNumbering { get; set; }

        [Filter("Run Time", FilterPropertyType.Number)]
        [JsonProperty("runtime")]
        public int Runtime { get; set; }

        [JsonProperty("tvdbId")]
        public int TvdbId { get; set; }

        [JsonProperty("tvRageId")]
        public int TvRageId { get; set; }

        [JsonProperty("tvMazeId")]
        public int TvMazeId { get; set; }

        [Filter("First Aired", FilterPropertyType.DateTime)]
        [JsonProperty("firstAired")]
        public DateTime FirstAired { get; set; }

        [JsonProperty("lastInfoSync")]
        public DateTime LastInfoSync { get; set; }

        [Filter("Series Type", FilterPropertyType.Enum)]
        [JsonProperty("seriesType")]
        public string SeriesType { get; set; }

        [JsonProperty("cleanTitle")]
        public string CleanTitle { get; set; }

        [JsonProperty("imdbId")]
        public string ImdbId { get; set; }

        [JsonProperty("titleSlug")]
        public string TitleSlug { get; set; }

        [Filter("Certification", FilterPropertyType.Enum)]
        [JsonProperty("certification")]
        public string Certification { get; set; }

        [JsonProperty("genres")]
        public List<string> Genres { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [Filter("Added", FilterPropertyType.DateTime)]
        [JsonProperty("added")]
        public DateTime Added { get; set; }

        [Filter("Ratings", true)]
        [JsonProperty("ratings")]
        public Ratings Ratings { get; set; }

        [JsonProperty("qualityProfileId")]
        public int QualityProfileId { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("airTime")]
        public string AirTime { get; set; }

        [JsonProperty("nextAiring")]
        public DateTime? NextAiring { get; set; }
    }


}
