using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Interfaces
{
    public interface IFFmpegPreset
    {
        public int? AudioBitRate { get; set; }
        public string AudioCodec { get; set; }
        public string Container { get; set; }

        [JsonIgnore]
        public string ContainerExtension { get; set; }

        public double? FrameRate { get; set; }
        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }
        public string VideoCodec { get; set; }

        public List<string> GetArgumentString();
    }
}