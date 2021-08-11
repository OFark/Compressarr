using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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

    public class SeasonJSON
    {
        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("monitored")]
        public bool Monitored { get; set; }

        [JsonProperty("statistics")]
        public Statistics Statistics { get; set; }

        public IEnumerable<EpisodeFile> EpisodeFiles { get; set; } = new HashSet<EpisodeFile>();
    }

    public class SeriesJSON
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("ended")]
        public bool Ended { get; set; }

        [JsonProperty("alternateTitles")]
        public List<AlternateTitle> AlternateTitles { get; set; }

        [JsonProperty("sortTitle")]
        public string SortTitle { get; set; }

        [JsonProperty("seasonCount")]
        public int SeasonCount { get; set; }

        [JsonProperty("totalEpisodeCount")]
        public int TotalEpisodeCount { get; set; }

        [JsonProperty("episodeCount")]
        public int EpisodeCount { get; set; }

        [JsonProperty("episodeFileCount")]
        public int EpisodeFileCount { get; set; }

        [JsonProperty("sizeOnDisk")]
        public long SizeOnDisk { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("previousAiring")]
        public DateTime PreviousAiring { get; set; }

        [JsonProperty("network")]
        public string Network { get; set; }

        [JsonProperty("images")]
        public List<Image> Images { get; set; }

        [JsonProperty("seasons")]
        public IEnumerable<SeasonJSON> Seasons { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

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

        [JsonProperty("runtime")]
        public int Runtime { get; set; }

        [JsonProperty("tvdbId")]
        public int TvdbId { get; set; }

        [JsonProperty("tvRageId")]
        public int TvRageId { get; set; }

        [JsonProperty("tvMazeId")]
        public int TvMazeId { get; set; }

        [JsonProperty("firstAired")]
        public DateTime FirstAired { get; set; }

        [JsonProperty("lastInfoSync")]
        public DateTime LastInfoSync { get; set; }

        [JsonProperty("seriesType")]
        public string SeriesType { get; set; }

        [JsonProperty("cleanTitle")]
        public string CleanTitle { get; set; }

        [JsonProperty("imdbId")]
        public string ImdbId { get; set; }

        [JsonProperty("titleSlug")]
        public string TitleSlug { get; set; }

        [JsonProperty("certification")]
        public string Certification { get; set; }

        [JsonProperty("genres")]
        public List<string> Genres { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("added")]
        public DateTime Added { get; set; }

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
