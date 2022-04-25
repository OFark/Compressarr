using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Compressarr.Services.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class AlternativeTitle
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("language")]
        public Language Language { get; set; }

        [JsonProperty("movieId")]
        public int MovieId { get; set; }

        [JsonProperty("sourceId")]
        public int SourceId { get; set; }

        [JsonProperty("sourceType")]
        public string SourceType { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("voteCount")]
        public int VoteCount { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }
    }

    public class Movie : Media, IMedia
    {
        [JsonProperty("added")]
        [Filter("Added", FilterPropertyType.DateTime)]
        public DateTime Added { get; set; }

        [JsonProperty("alternativeTitles")]
        public HashSet<AlternativeTitle> AlternativeTitles { get; set; }

        [JsonProperty("string")]
        [Filter("Clean Title")]
        public string CleanTitle { get; set; }

        [JsonIgnore]
        public new string FilePath => $"{BasePath}{System.IO.Path.Combine(Path, MovieFile?.RelativePath)}";

        [JsonProperty("folderName")]
        [Filter("Folder Name", FilterPropertyType.Enum)]
        public string FolderName { get; set; }

        [JsonProperty("genres")]
        public HashSet<object> Genres { get; set; }

        [JsonProperty("hasFile")]
        [Filter("Has File", FilterPropertyType.Boolean)]
        public bool HasFile { get; set; }

        [JsonProperty("images")]
        public HashSet<Image> Images { get; set; }

        [JsonProperty("imdbId")]
        [Filter("IMDB ID")]
        public string ImdbId { get; set; }

        [JsonProperty("inCinemas")]
        [Filter("In Cinemas", FilterPropertyType.DateTime)]
        public DateTime InCinemas { get; set; }

        [JsonProperty("isAvailable")]
        [Filter("Is Available", FilterPropertyType.Boolean)]
        public bool IsAvailable { get; set; }

        [JsonProperty("lastInfoSync")]
        [Filter("Last Info Sync", FilterPropertyType.DateTime)]
        public DateTime LastInfoSync { get; set; }

        [JsonProperty("minimumAvailability")]
        [Filter("Minimum Availability", FilterPropertyType.Enum)]
        public string MinimumAvailability { get; set; }

        [JsonProperty("monitored")]
        [Filter("Monitored", FilterPropertyType.Boolean)]
        public bool Monitored { get; set; }

        [JsonProperty("movieFile")]
        [Filter("Movie File", true)]
        public MovieFile MovieFile { get; set; }

        [JsonProperty("overview")]
        [Filter("Overview")]
        public string Overview { get; set; }

        [JsonProperty("path")]
        [Filter("Path")]
        public string Path { get; set; }

        [JsonProperty("pathState")]
        [Filter("PathState", FilterPropertyType.Enum)]
        public string PathState { get; set; }

        [JsonProperty("physicalRelease")]
        [Filter("Physical Release", FilterPropertyType.DateTime)]
        public DateTime PhysicalRelease { get; set; }

        [JsonProperty("profileId")]
        [Filter("Profile ID", FilterPropertyType.Number)]
        public int ProfileId { get; set; }

        [JsonProperty("qualityProfileId")]
        [Filter("Quality Profile ID", FilterPropertyType.Number)]
        public int QualityProfileId { get; set; }

        [JsonProperty("ratings")]
        [Filter("Ratings", true)]
        public Ratings Ratings { get; set; }

        [JsonProperty("runtime")]
        [Filter("Runtime", FilterPropertyType.Number)]
        public int Runtime { get; set; }

        [JsonProperty("secondaryYearSourceId")]
        public int SecondaryYearSourceId { get; set; }

        [JsonIgnore]
        public bool ShowHistory { get; set; }

        [JsonIgnore]
        public bool ShowInfo { get; set; }

        [JsonProperty("sizeOnDisk")]
        [Filter("Size on disk", FilterPropertyType.Number)]
        public long SizeOnDisk { get; set; }

        [JsonProperty("sortTitle")]
        [Filter("Sort Title")]
        public string SortTitle { get; set; }

        [JsonProperty("status")]
        [Filter("Status", FilterPropertyType.Enum)]
        public string Status { get; set; }

        [JsonProperty("studio")]
        [Filter("Studio")]
        public string Studio { get; set; }

        [JsonProperty("tags")]
        public HashSet<int> Tags { get; set; }

        [JsonProperty("title")]
        [Filter("Title")]
        public string Title { get; set; }

        [JsonProperty("titleSlug")]
        [Filter("Title Slug")]
        public string TitleSlug { get; set; }

        [JsonProperty("tmdbId")]
        [Filter("TMDB ID", FilterPropertyType.Number)]
        public int TmdbId { get; set; }

        [JsonProperty("website")]
        [Filter("Website")]
        public string Website { get; set; }

        [JsonProperty("year")]
        [Filter("Year", FilterPropertyType.Number)]
        public int Year { get; set; }

        [JsonProperty("youTubeTrailerId")]
        [Filter("YouTube Trailer ID")]
        public string YouTubeTrailerId { get; set; }
    }

    public class MovieFile
    {

        [JsonProperty("dateAdded")]
        [Filter("Date Added", FilterPropertyType.DateTime)]
        public DateTime DateAdded { get; set; }

        [JsonProperty("edition")]
        [Filter("Edition")]
        public string Edition { get; set; }

        [JsonProperty("id")]
        [Filter("ID", FilterPropertyType.Number)]
        public int Id { get; set; }

        [JsonProperty("mediaInfo")]
        [Filter("Media Info", true)]
        public MovieMediaInfo MediaInfo { get; set; }

        [JsonProperty("movieId")]
        public int MovieId { get; set; }

        [JsonProperty("quality")]
        [Filter("Quality", true)]
        public MovieQuality Quality { get; set; }

        [JsonProperty("relativePath")]
        [Filter("Relative Path")]
        public string RelativePath { get; set; }

        [JsonProperty("releaseGroup")]
        [Filter("Release Group", FilterPropertyType.Enum)]
        public string ReleaseGroup { get; set; }

        [JsonProperty("sceneName")]
        [Filter("Scene Name", FilterPropertyType.Enum)]
        public string SceneName { get; set; }

        [JsonProperty("size")]
        [Filter("Size", FilterPropertyType.Number)]
        public long Size { get; set; }

        [Filter("Size(F)", FilterPropertyType.FileSize, FilterOn = "Size")]
        public string SizeNice => Size.ToFileSize();

        public class FileQuality
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [Filter("Modifier", FilterPropertyType.Enum)]
            [JsonProperty("modifier")]
            public string Modifier { get; set; }

            [Filter("Name", FilterPropertyType.Enum)]
            [JsonProperty("name")]
            public string Name { get; set; }

            [Filter("Resolution", FilterPropertyType.Enum)]
            [JsonProperty("resolution")]
            public int Resolution { get; set; }

            [Filter("Source", FilterPropertyType.Enum)]
            [JsonProperty("source")]
            public string Source { get; set; }
        }

        public class MovieQuality
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

    public class MovieMediaInfo
    {
        [JsonProperty("audioBitrate")]
        [Filter("Audio - Bitrate", FilterPropertyType.Number)]
        public int AudioBitrate { get; set; }

        [JsonProperty("audioChannels")]
        [Filter("Audio - Channels", FilterPropertyType.Number)]
        public decimal AudioChannels { get; set; }

        [JsonProperty("audioCodec")]
        [Filter("Audio - Codec", FilterPropertyType.Enum)]
        public string AudioCodec { get; set; }

        [JsonProperty("audioLanguages")]
        [Filter("Audio - Languages", FilterPropertyType.Enum)]
        public string AudioLanguages { get; set; }

        [JsonProperty("audioStreamCount")]
        [Filter("Audio - Stream Count", FilterPropertyType.Number)]
        public int AudioStreamCount { get; set; }

        [Filter("Height", FilterPropertyType.Number)]
        public int Height => string.IsNullOrWhiteSpace(Resolution) ? 0 : Resolution.Split("x").Length == 2 ? int.Parse(Resolution.Split("x")[1]) : 0;

        [JsonProperty("runTime")]
        [Filter("Run Time")]
        public string RunTime { get; set; }

        [JsonProperty("resolution")]
        [Filter("Resolution")]
        public string Resolution { get; set; }

        [Filter("Run Time(F)", FilterOn = "RunTime")]
        public string RunTimeNice
        {
            get
            {
                var reg = new Regex(@"\d+(?=[:.])");
                var matches = reg.Matches(RunTime);
                if (matches.Count == 3)
                {
                    return $"{int.Parse(matches[0].Value)}:{matches[1].Value}:{matches[2].Value}";
                }
                return null;
            }
        }

        [JsonProperty("scanType")]
        [Filter("Scan Type", FilterPropertyType.Enum)]
        public string ScanType { get; set; }

        [JsonProperty("subtitles")]
        [Filter("Subtitles")]
        public string Subtitles { get; set; }

        [JsonProperty("videoBitDepth")]
        [Filter("Video - Bit Depth", FilterPropertyType.Number)]
        public int VideoBitDepth { get; set; }

        [JsonProperty("videoBitrate")]
        [Filter("Video - Bitrate", FilterPropertyType.Number)]
        public int VideoBitrate { get; set; }

        [Filter("Video - Bitrate(F)", FilterOn = "VideoBitrate")]
        public string VideoBitrateNice => VideoBitrate.ToBitRate();

        [JsonProperty("videoCodec")]
        [Filter("Video - Codec", FilterPropertyType.Enum)]
        public string VideoCodec { get; set; }

        [JsonProperty("videoDynamicRangeType")]
        [Filter("Video - Dynamic Range Type", FilterPropertyType.Enum)]
        public string VideoDynamicRangeType { get; set; }

        [Filter("Video - Data Rate", FilterPropertyType.Number, Suffix = "bpp")]
        public decimal VideoDataRate
        {
            get
            {
                if (VideoBitrate > 0 && VideoFps > 0 && Width > 0 && Height > 0 && VideoBitDepth > 0)
                {
                    return Math.Round(VideoBitrate / VideoFps / Width / Height / VideoBitDepth, 3);
                }

                return -1;
            }
        }

        [JsonProperty("videoFps")]
        [Filter("Video - FPS", FilterPropertyType.Number)]
        public decimal VideoFps { get; set; }
        
        [JsonProperty("width")]
        [Filter("Width", FilterPropertyType.Number)]
        public int Width => string.IsNullOrWhiteSpace(Resolution) ? 0 : Resolution.Split("x").Length == 2 ? int.Parse(Resolution.Split("x")[0]) : 0;
    }
}