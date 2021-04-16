using Compressarr.FFmpegFactory;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegPreset : IFFmpegPreset
    {
        public int? AudioBitRate { get; set; }
        public string AudioCodec { get; set; }
        public string Container { get; set; }
        public string ContainerExtension { get; set; }
        public double? FrameRate { get; set; }
        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }
        public string VideoCodec { get; set; }

        public HashSet<CodecOptionValue> VideoCodecOptions { get; set; }


        private string videoCodecOptions
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
                        else if(!string.IsNullOrWhiteSpace(vco.Value))
                        {
                            sb.Append($" {vco.Arg.Replace("<val>", $"{vco.Value}".Trim())}");
                        }
                    }
                }

                return sb.ToString();
            }
        }

        public virtual List<string> GetArgumentString()
        {
            List<string> args = new List<string>();

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

                var part1Ending = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL && ^" : @"/dev/null && \";

                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec}{videoCodecOptions} -b:v {VideoBitRate}k{frameRate}{passStr} -an -f null {part1Ending}".Replace("%passnum%", "1"));
                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec}{videoCodecOptions} -b:v {VideoBitRate}k{frameRate}{passStr} -c:a {AudioCodec}{audioBitrate}{opArgsStr} \"{{1}}\"".Replace("%passnum%", "2"));
            }
            else
            {
                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec}{frameRate}{videoCodecOptions} -c:a {AudioCodec}{audioBitrate}{opArgsStr} \"{{1}}\"");
            }

            return args;
        }
    }
}