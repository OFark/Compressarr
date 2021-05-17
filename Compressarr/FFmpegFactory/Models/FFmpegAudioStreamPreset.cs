﻿using Compressarr.Settings.FFmpegFactory;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegAudioStreamPreset :FFmpegAudioStreamPresetBase
    {
        public FFmpegAudioStreamPreset()
        {
            Filters ??= new() { new() };
        }

        public FFmpegAudioStreamPreset(FFmpegAudioStreamPresetBase audioStreamPresetBase)
        {
            Action = audioStreamPresetBase?.Action ?? default;
            BitRate = audioStreamPresetBase?.BitRate;
            Encoder = audioStreamPresetBase.Encoder != null ? new Encoder(audioStreamPresetBase.Encoder) : null;
            Filters = audioStreamPresetBase?.Filters?.Select(x => new FFmpegAudioStreamPresetFilter(x)).ToList();
        }

        public new Encoder Encoder { get; set; }

        public new List<FFmpegAudioStreamPresetFilter> Filters { get; set; }

        public bool CoversAny => Filters.Any(f => f.Rule == AudioStreamRule.Any);
    }
}
