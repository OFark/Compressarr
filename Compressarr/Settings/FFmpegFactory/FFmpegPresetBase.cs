using Compressarr.Presets.Models;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Settings.FFmpegFactory
{
    public class FFmpegPresetBase
    {
        public FFmpegPresetBase()
        {

        }
        public FFmpegPresetBase(FFmpegPreset preset)
        {
            AudioStreamPresets = preset.AudioStreamPresets.Select(x => new FFmpegAudioStreamPresetBase(x)).ToList();
            Container = preset.Container;
            FrameRate = preset.FrameRate;
            HardwareDecoder = preset.HardwareDecoder;
            Name = preset.Name;
            OptionalArguments = preset.OptionalArguments;
            VideoBitRate = preset.VideoBitRate;
            VideoCodecOptions = preset.VideoCodecOptions.Select(x => new EncoderOptionValueBase(x)).ToHashSet();
            VideoEncoder = new(preset.VideoEncoder);
        }

        public List<FFmpegAudioStreamPresetBase> AudioStreamPresets { get; set; }

        public string Container { get; set; }

        public double? FrameRate { get; set; }
        public string HardwareDecoder { get; set; }

        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }
        public HashSet<EncoderOptionValueBase> VideoCodecOptions { get; set; }

        public EncoderBase VideoEncoder { get; set; }
    }
}