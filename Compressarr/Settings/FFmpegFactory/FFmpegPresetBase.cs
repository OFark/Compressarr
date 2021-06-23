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
            B_Frames = preset.B_Frames;
            Container = preset.Container;
            FrameRate = preset.FrameRate;
            HardwareDecoder = preset.HardwareDecoder;
            Name = preset.Name;
            OptionalArguments = preset.OptionalArguments;
            VideoBitRate = preset.VideoBitRate;
            VideoEncoderOptions = preset.VideoEncoderOptions?.Select(x => new EncoderOptionValueBase(x)).ToHashSet();
            VideoEncoder = new(preset.VideoEncoder);
        }

        public List<FFmpegAudioStreamPresetBase> AudioStreamPresets { get; set; }

        public string Container { get; set; }

        public double? FrameRate { get; set; }
        public string HardwareDecoder { get; set; }

        public string Name { get; set; }
        public string OptionalArguments { get; set; }
        public int? VideoBitRate { get; set; }

        public int B_Frames { get; set; }
        public HashSet<EncoderOptionValueBase> VideoEncoderOptions { get; set; }

        public EncoderBase VideoEncoder { get; set; }
    }
}