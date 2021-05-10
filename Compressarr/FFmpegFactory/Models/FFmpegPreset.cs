using Compressarr.FFmpegFactory;
using Compressarr.Helpers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegPreset
    {
        private Codec _videoCodec;

        public FFmpegPreset()
        {
            AudioCodec ??= new();
        }

        [JsonIgnore]
        public List<string> Arguments
        {
            get
            {
                List<string> args = new();

                var audioBitrate = AudioBitRate.HasValue ? $" -b:a {AudioBitRate}k" : "";
                var frameRate = FrameRate.HasValue ? $" -r {FrameRate}" : "";
                var opArgsStr = string.IsNullOrWhiteSpace(OptionalArguments) ? "" : $" {OptionalArguments.Trim()}";
                var passStr = " -pass %passnum%";

                if (VideoBitRate.HasValue)
                {
                    if (VideoCodecOptions != null)
                    {
                        if (VideoCodecOptions.Any(vco => vco.IncludePass))
                        {
                            passStr = string.Empty;
                        }
                    }

                    var part1Ending = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : @"/dev/null";

                    args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec.Name}{VideoCodecParams} -b:v {VideoBitRate}k{frameRate}{passStr} -an -f null {part1Ending}".Replace("%passnum%", "1"));
                    args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec.Name}{VideoCodecParams} -b:v {VideoBitRate}k{frameRate}{passStr} -c:a {AudioCodec.Name}{audioBitrate}{opArgsStr} \"{{1}}\"".Replace("%passnum%", "2"));
                }
                else
                {
                    args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec.Name}{frameRate}{VideoCodecParams} -c:a {AudioCodec.Name}{audioBitrate}{opArgsStr} \"{{1}}\"");
                }

                return args;
            }
        }

        public int? AudioBitRate { get; set; }
        public Codec AudioCodec { get; set; }
        public string Container { get; set; }

        [JsonIgnore]
        public string ContainerExtension { get; set; }
        public double? FrameRate { get; set; }
        [JsonIgnore]
        public bool Initialised { get; set; }

        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }
        public Codec VideoCodec
        {
            get
            {
                return _videoCodec ?? new();
            }
            set
            {
                if (_videoCodec != null && _videoCodec.Name != value.Name)
                {
                    VideoCodecOptions = value?.Options?.WithValues();
                }
                else
                {
                    VideoCodecOptions = value?.Options?.WithValues(VideoCodecOptions);
                }

                _videoCodec = value;

            }
        }

        public HashSet<CodecOptionValue> VideoCodecOptions { get; set; }

        public override string ToString()
        {
            return string.Join(" | ", (new List<string>() { VideoCodec?.Name, Name }).Where(x =>!string.IsNullOrWhiteSpace(x)));
        }

        private string VideoCodecParams
        {
            get
            {
                var sb = new StringBuilder();
                if (VideoCodecOptions != null)
                {
                    foreach (var vco in VideoCodecOptions)
                    {
                        if (VideoBitRate.HasValue)
                        {
                            if (vco.IncludePass)
                            {
                                sb.Append($" {vco.Arg.Replace("<val>", $"{vco.Value} pass=%passnum%".Trim())}");
                            }
                            else if (!string.IsNullOrWhiteSpace(vco.Value))
                            {
                                if (!vco.DisabledByVideoBitRate)
                                {
                                    sb.Append($" {vco.Arg.Replace("<val>", $"{vco.Value}".Trim())}");
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(vco.Value))
                        {
                            sb.Append($" {vco.Arg.Replace("<val>", $"{vco.Value}".Trim())}");
                        }
                    }
                }

                return sb.ToString();
            }
        }
    }
}