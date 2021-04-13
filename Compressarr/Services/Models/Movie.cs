using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        public string modifier { get; set; }
        public string name { get; set; }
        public string resolution { get; set; }
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
        public string audioAdditionalFeatures { get; set; }
        public int audioBitrate { get; set; }
        public string audioChannelPositions { get; set; }
        public string audioChannelPositionsText { get; set; }
        public int audioChannels { get; set; }
        public string audioCodecID { get; set; }
        public string audioCodecLibrary { get; set; }
        public string audioFormat { get; set; }
        public string audioLanguages { get; set; }
        public string audioProfile { get; set; }
        public int audioStreamCount { get; set; }
        public string containerFormat { get; set; }
        public int height { get; set; }
        public string runTime { get; set; }

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

        public string scanType { get; set; }
        public int schemaRevision { get; set; }
        public string subtitles { get; set; }
        public int videoBitDepth { get; set; }
        public int videoBitrate { get; set; }
        public string videoCodec => string.IsNullOrWhiteSpace(videoCodecLibrary) ? "Unknown" : videoCodecLibrary.Split(" ")[0];
        public string videoCodecID { get; set; }
        public string videoCodecLibrary { get; set; }
        public string videoColourPrimaries { get; set; }

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

        public string videoFormat { get; set; }
        public decimal videoFps { get; set; }
        public int videoMultiViewCount { get; set; }
        public string videoProfile { get; set; }
        public string videoTransferCharacteristics { get; set; }
        public int width { get; set; }
    }

    public class Movie
    {
        public DateTime added { get; set; }
        public HashSet<AlternativeTitle> alternativeTitles { get; set; }
        public string cleanTitle { get; set; }
        public bool downloaded { get; set; }
        public string folderName { get; set; }
        public HashSet<object> genres { get; set; }
        public bool hasFile { get; set; }
        public int id { get; set; }
        public HashSet<Image> images { get; set; }
        public string imdbId { get; set; }
        public DateTime inCinemas { get; set; }
        public bool isAvailable { get; set; }
        public DateTime lastInfoSync { get; set; }
        public string minimumAvailability { get; set; }
        public bool monitored { get; set; }
        public MovieFile movieFile { get; set; }
        public string overview { get; set; }
        public string path { get; set; }
        public string pathState { get; set; }
        public DateTime physicalRelease { get; set; }
        public int profileId { get; set; }
        public int qualityProfileId { get; set; }
        public Ratings ratings { get; set; }
        public int runtime { get; set; }
        public int secondaryYearSourceId { get; set; }
        public long sizeOnDisk { get; set; }
        public string sortTitle { get; set; }
        public string status { get; set; }
        public string studio { get; set; }
        public HashSet<int> tags { get; set; }
        public string title { get; set; }
        public string titleSlug { get; set; }
        public int tmdbId { get; set; }
        public string website { get; set; }
        public int year { get; set; }
        public string youTubeTrailerId { get; set; }
    }

    public class MovieFile
    {
        public DateTime dateAdded { get; set; }
        public string edition { get; set; }
        public int id { get; set; }
        public MediaInfo mediaInfo { get; set; }
        public int movieId { get; set; }
        public Quality quality { get; set; }
        public string relativePath { get; set; }
        public string releaseGroup { get; set; }
        public string sceneName { get; set; }
        public long size { get; set; }
    }

    public class Quality
    {
        public List<object> customFormats { get; set; }
        public FileQuality quality { get; set; }
        public Revision revision { get; set; }
    }

    public class Ratings
    {
        public double value { get; set; }
        public int votes { get; set; }
    }

    public class Revision
    {
        public int real { get; set; }
        public int version { get; set; }
    }
}