using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xabe.FFmpeg;

namespace Compressarr.Services.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class AlternativeTitle
    {
        public int id { get; set; }
        public Language language { get; set; }
        public int movieId { get; set; }
        public int sourceId { get; set; }
        public string sourceType { get; set; }
        public string title { get; set; }
        public int voteCount { get; set; }
        public int votes { get; set; }
    }

    public class FileQuality
    {
        public int id { get; set; }

        [Filter("Modifier", FilterPropertyType.Enum)]
        public string modifier { get; set; }

        [Filter("Name", FilterPropertyType.Enum)]
        public string name { get; set; }

        [Filter("Resolution", FilterPropertyType.Enum)]
        public int resolution { get; set; }

        [Filter("Source", FilterPropertyType.Enum)]
        public string source { get; set; }
    }

    public class Image
    {
        public string coverType { get; set; }
        public string url { get; set; }
    }

    public class Language
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class MediaInfo
    {
        [Filter("Audio - Additional Features")]
        public string audioAdditionalFeatures { get; set; }

        [Filter("Audio - Bitrate", FilterPropertyType.Number)]
        public int audioBitrate { get; set; }

        [Filter("Audio - Channel Positions")]
        public string audioChannelPositions { get; set; }

        [Filter("Audio - Channel Positions Text")]
        public string audioChannelPositionsText { get; set; }

        [Filter("Audio - Channels", FilterPropertyType.Number)]
        public int audioChannelsContainer { get; set; }

        [Filter("Audio - Codec ID", FilterPropertyType.Enum)]
        public string audioCodecID { get; set; }

        [Filter("Audio - Codec Library", FilterPropertyType.Enum)]
        public string audioCodecLibrary { get; set; }
        
        [Filter("Audio - Format", FilterPropertyType.Enum)]
        public string audioFormat { get; set; }

        [Filter("Audio - Languages", FilterPropertyType.Enum)]
        public string audioLanguages { get; set; }

        [Filter("Audio - Profile", FilterPropertyType.Enum)]
        public string audioProfile { get; set; }

        [Filter("Audio - Stream Count", FilterPropertyType.Number)]
        public int audioStreamCount { get; set; }

        [Filter("Container", FilterPropertyType.Enum)]
        public string containerFormat { get; set; }

        [Filter("Height", FilterPropertyType.Number)]
        public int height { get; set; }

        [Filter("Run Time")]
        public string runTime { get; set; }

        [Filter("Run Time(F)", FilterOn ="runTime")]
        public string runTimeNice
        {
            get
            {
                var reg = new Regex(@"\d+(?=[:.])");
                var matches = reg.Matches(runTime);
                if (matches.Count == 3)
                {
                    return $"{int.Parse(matches[0].Value)}:{matches[1].Value}:{matches[2].Value}";
                }
                return null;
            }
        }

        [Filter("Scan Type", FilterPropertyType.Enum)]
        public string scanType { get; set; }

        [Filter("Schema Revision", FilterPropertyType.Number)]
        public int schemaRevision { get; set; }

        [Filter("Subtitles")]
        public string subtitles { get; set; }

        [Filter("Video - Bit Depth", FilterPropertyType.Number)]
        public int videoBitDepth { get; set; }

        [Filter("Video - Bitrate", FilterPropertyType.Number)]
        public int videoBitrate { get; set; }

        [Filter("Video - Bitrate(F)", FilterOn = "videoBitrate")]
        public string videoBitrateNice => videoBitrate.ToBitRate();

        [Filter("Video - Codec", FilterPropertyType.Enum)]
        public string videoCodec => string.IsNullOrWhiteSpace(videoCodecLibrary) ? "Unknown" : videoCodecLibrary.Split(" ")[0];

        [Filter("Video - Codec ID", FilterPropertyType.Enum)]
        public string videoCodecID { get; set; }
        [Filter("Video - Codec Library", FilterPropertyType.Enum)]
        public string videoCodecLibrary { get; set; }

        [Filter("Video - Codec Primaries", FilterPropertyType.Enum)]
        public string videoColourPrimaries { get; set; }

        [Filter("Video - Data Rate", FilterPropertyType.Number, Suffix = "bpp")]
        public decimal videoDataRate
        {
            get
            {
                if (videoBitrate > 0 && videoFps > 0 && width > 0 && height > 0 && videoBitDepth > 0)
                {
                    return Math.Round(videoBitrate / videoFps / width / height / videoBitDepth, 3);
                }

                return -1;
            }
        }

        [Filter("Video - Format", FilterPropertyType.Enum)]
        public string videoFormat { get; set; }

        [Filter("Video - FPS", FilterPropertyType.Number)]
        public decimal videoFps { get; set; }

        [Filter("Video - Multi View Count", FilterPropertyType.Number)]
        public int videoMultiViewCount { get; set; }

        [Filter("Video - Profile", FilterPropertyType.Enum)]
        public string videoProfile { get; set; }

        [Filter("Video - Transfer Characteristics", FilterPropertyType.Enum)]
        public string videoTransferCharacteristics { get; set; }

        [Filter("Width", FilterPropertyType.Number)]
        public int width { get; set; }
    }

    public class Movie
    {
        [Filter("Added", FilterPropertyType.DateTime)]
        public DateTime added { get; set; }

        public HashSet<AlternativeTitle> alternativeTitles { get; set; }

        [Filter("Clean Title")]
        public string cleanTitle { get; set; }

        [Filter("Downloaded", FilterPropertyType.Boolean)]
        public bool downloaded { get; set; }

        [Filter("Folder Name", FilterPropertyType.Enum)]
        public string folderName { get; set; }

        public HashSet<object> genres { get; set; }

        [Filter("Has File", FilterPropertyType.Boolean)]
        public bool hasFile { get; set; }

        [Filter("ID", FilterPropertyType.Number)]
        public int id { get; set; }

        public HashSet<Image> images { get; set; }

        [Filter("IMDB ID")]
        public string imdbId { get; set; }

        [Filter("In Cinemas", FilterPropertyType.DateTime)]
        public DateTime inCinemas { get; set; }

        [Filter("Is Available", FilterPropertyType.Boolean)]
        public bool isAvailable { get; set; }

        [Filter("Last Info Sync", FilterPropertyType.DateTime)]
        public DateTime lastInfoSync { get; set; }

        [Filter("Minimum Availability", FilterPropertyType.Enum)]
        public string minimumAvailability { get; set; }

        [Filter("Monitored", FilterPropertyType.Boolean)]
        public bool monitored { get; set; }

        [Filter("Movie File", true)]
        public MovieFile movieFile { get; set; }

        [Filter("Overview")]
        public string overview { get; set; }

        [Filter("Path")]
        public string path { get; set; }

        [Filter("PathState", FilterPropertyType.Enum)]
        public string pathState { get; set; }

        [Filter("Physical Release", FilterPropertyType.DateTime)]
        public DateTime physicalRelease { get; set; }

        [Filter("Profile ID", FilterPropertyType.Number)]
        public int profileId { get; set; }

        [Filter("Quality Profile ID", FilterPropertyType.Number)]
        public int qualityProfileId { get; set; }

        [Filter("Ratings", true)]
        public Ratings ratings { get; set; }

        [Filter("Runtime", FilterPropertyType.Number)]
        public int runtime { get; set; }

        public int secondaryYearSourceId { get; set; }

        [Filter("Size on disk", FilterPropertyType.Number)]
        public long sizeOnDisk { get; set; }

        [Filter("Sort Title")]
        public string sortTitle { get; set; }

        [Filter("Status", FilterPropertyType.Enum)]
        public string status { get; set; }

        [Filter("Studio")]
        public string studio { get; set; }

        public HashSet<int> tags { get; set; }

        [Filter("Title")]
        public string title { get; set; }

        [Filter("Title Slug")]
        public string titleSlug { get; set; }

        [Filter("TMDB ID", FilterPropertyType.Number)]
        public int tmdbId { get; set; }

        [Filter("Website")]
        public string website { get; set; }

        [Filter("Year", FilterPropertyType.Number)]
        public int year { get; set; }

        [Filter("YouTube Trailer ID")]
        public string youTubeTrailerId { get; set; }

        [JsonIgnore]
        public FFProbeResponse MediaInfo { get; set; }

        [JsonIgnore]
        public bool ShowInfo { get; set; }

        public int GetStableHash()
        {
            return JsonConvert.SerializeObject(this).GetStableHashCode();
        }
    }

    public class MovieFile
    {
        [Filter("Date Added", FilterPropertyType.DateTime)]
        public DateTime dateAdded { get; set; }

        [Filter("Edition")]
        public string edition { get; set; }

        [Filter("ID", FilterPropertyType.Number)]
        public int id { get; set; }

        [Filter("Media Info", true)]
        public MediaInfo mediaInfo { get; set; }

        public int movieId { get; set; }

        [Filter("Quality", true)]
        public Quality quality { get; set; }

        [Filter("Relative Path")]
        public string relativePath { get; set; }

        [Filter("Release Group", FilterPropertyType.Enum)]
        public string releaseGroup { get; set; }

        [Filter("Scene Name", FilterPropertyType.Enum)]
        public string sceneName { get; set; }

        [Filter("Size", FilterPropertyType.Number)]
        public long size { get; set; }

        [Filter("Size(F)", FilterPropertyType.Number, FilterOn = "size")]
        public string sizeNice => size.ToFileSize();
    }

    public class Quality
    {
        public List<object> customFormats { get; set; }

        [Filter("File Quality", true)]
        public FileQuality quality { get; set; }

        [Filter("Revision", true)]
        public Revision revision { get; set; }
    }

    public class Ratings
    {
        [Filter("Value", FilterPropertyType.Number)]
        public double value { get; set; }

        [Filter("Votes", FilterPropertyType.Number)]
        public int votes { get; set; }
    }

    public class Revision
    {
        [Filter("Real", FilterPropertyType.Number)]
        public int real { get; set; }

        [Filter("Version", FilterPropertyType.Number)]
        public int version { get; set; }

        [Filter("Is Repack", FilterPropertyType.Boolean)]
        public bool isRepack { get; set; }
    }
}