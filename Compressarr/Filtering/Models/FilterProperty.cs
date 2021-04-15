using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Filtering.Models
{
    public class FilterProperty
    {
        public static Dictionary<string, string> valueNames = new Dictionary<string, string>()
        {
            {"Name", "title" },
            { "Width", "movieFile.mediaInfo.width" },
            { "Height", "movieFile.mediaInfo.height" },
            { "Codec", "movieFile.mediaInfo.videoCodec" },
            { "Codec ID", "movieFile.mediaInfo.videoCodecID" },
            { "Format", "movieFile.mediaInfo.videoFormat" },
            { "Container", "movieFile.mediaInfo.containerFormat" },
            { "Bit Depth", "movieFile.mediaInfo.videoBitDepth" },
            { "Bit Rate", "movieFile.mediaInfo.videoBitrate" },
            { "FPS", "movieFile.mediaInfo.videoFps" },
            { "Data Rate", "movieFile.mediaInfo.videoDataRate" },
            { "Audio Format", "movieFile.mediaInfo.audioFormat" },
            { "Audio Codec", "movieFile.mediaInfo.audioCodecID" }
        };

        public FilterProperty(string name, string value, FilterPropertyType propertyType, string suffix = null, string filterOn = null)
        {
            Key = name;
            Value = value;
            PropertyType = propertyType;
            Suffix = suffix;
            FilterOn = filterOn;
        }

        public string Key { get; set; }
        //{
        //    get
        //    {
        //        KeyValuePair<string, string> def = default;
        //        var kpvalue = valueNames.FirstOrDefault(x => x.Value == Value);
        //        if (!kpvalue.Equals(def))
        //        {
        //            return kpvalue.Key;
        //        }

        //        return Value;
        //    }
        //}

        [JsonIgnore]
        public string Name => Key.Split(" - ").LastOrDefault();

        public string Value { get; set; }

        [JsonIgnore]
        public string Suffix { get; set; }

        [JsonIgnore]
        public string FilterOn { get; set; }

        public FilterPropertyType PropertyType { get; set; }
    }
}