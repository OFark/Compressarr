using Compressarr.Application;
using Compressarr.Settings.FFmpegFactory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Presets.Models
{
    public class FFmpegAudioStreamPreset :FFmpegAudioStreamPresetBase, ICloneable<FFmpegAudioStreamPreset>
    {
        public FFmpegAudioStreamPreset()
        {
            Filters ??= new() { new() { Rule = AudioStreamRule.Any } };
        }

        public FFmpegAudioStreamPreset(FFmpegAudioStreamPresetBase audioStreamPresetBase)
        {
            Action = audioStreamPresetBase?.Action ?? default;
            BitRate = audioStreamPresetBase?.BitRate;
            Encoder = audioStreamPresetBase.Encoder != null ? new Encoder(audioStreamPresetBase.Encoder) : null;
            Filters = audioStreamPresetBase?.Filters?.Select(x => new FFmpegAudioStreamPresetFilter(x)).ToList();
        }

        public FFmpegAudioStreamPreset Clone()
        {
            var duplicate = new FFmpegAudioStreamPreset()
            {
                Action = Action,
                BitRate = BitRate,
                Encoder = Encoder?.Clone(),
                Filters = Filters?.ConvertAll(x => x.Clone())
            };

            return duplicate;
        }

        public new Encoder Encoder { get; set; }

        public new List<FFmpegAudioStreamPresetFilter> Filters { get; set; }

        public bool CoversAny => Filters.Any(f => f.Rule == AudioStreamRule.Any);
    }
}
