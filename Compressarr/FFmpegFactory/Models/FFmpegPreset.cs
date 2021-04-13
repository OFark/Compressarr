using Compressarr.FFmpegFactory.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xabe.FFmpeg;

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

        public virtual List<string> GetArgumentString()
        {
            List<string> args = new List<string>();

            var audioBitrate = AudioBitRate.HasValue ? $" -b:a {AudioBitRate}k" : "";
            var frameRate = FrameRate.HasValue ? $" -r {FrameRate}" : "";
            var opArgsStr = string.IsNullOrWhiteSpace(OptionalArguments) ? "" : $" {OptionalArguments.Trim()}";

            if (VideoBitRate.HasValue)
            {
                var part1Ending = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL && ^" : @"/dev/null && \";

                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec} -b:v {VideoBitRate}k{frameRate} -pass 1 -an -f null {part1Ending}");
                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec} -b:v {VideoBitRate}k{frameRate} -pass 2 -c:a {AudioCodec}{audioBitrate}{opArgsStr} \"{{1}}\"");
            }
            else
            {
                args.Add($"-y -i \"{{0}}\" -c:v {VideoCodec}{frameRate} -c:a {AudioCodec}{audioBitrate}{opArgsStr} \"{{1}}\"");
            }

            return args;
        }
    }
}