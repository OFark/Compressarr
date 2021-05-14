using Compressarr.Helpers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegPreset
    {
        private Encoder _videoEncoder;

        public FFmpegPreset()
        {
            
        }

        public FFmpegPreset(bool init)
        {
            AudioStreamPresets ??= new() { new(true) };
        }

        public List<FFmpegAudioStreamPreset> AudioStreamPresets { get; set; }

        public string Container { get; set; }

        [JsonIgnore]
        public string ContainerExtension { get; set; }
        public double? FrameRate { get; set; }
        [JsonIgnore]
        public bool Initialised { get; set; }

        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }
        public Encoder VideoEncoder
        {
            get
            {
                return _videoEncoder ?? new();
            }
            set
            {
                if (_videoEncoder != null && _videoEncoder.Name != value.Name)
                {
                    VideoCodecOptions = value?.Options?.WithValues();
                }
                else
                {
                    VideoCodecOptions = value?.Options?.WithValues(VideoCodecOptions);
                }

                _videoEncoder = value;

            }
        }

        public HashSet<EncoderOptionValue> VideoCodecOptions { get; set; }

        public override string ToString()
        {
            return string.Join(" | ", (new List<string>() { VideoEncoder?.Name, Name }).Where(x =>!string.IsNullOrWhiteSpace(x)));
        }

        [JsonIgnore]
        internal string VideoCodecParams
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