﻿using Compressarr.Helpers;
using Compressarr.Settings.FFmpegFactory;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Compressarr.Presets.Models
{
    public class FFmpegPreset : FFmpegPresetBase
    {
        private Encoder _videoEncoder;

        public FFmpegPreset()
        {
            AudioStreamPresets ??= new() { new() };
        }


        public FFmpegPreset(FFmpegPresetBase presetBase)
        {
            AudioStreamPresets = presetBase?.AudioStreamPresets?.Select(x => new FFmpegAudioStreamPreset(x)).ToList();
            Container = presetBase?.Container;
            FrameRate = presetBase?.FrameRate;
            HardwareDecoder = presetBase?.HardwareDecoder;
            Name = presetBase?.Name;
            OptionalArguments = presetBase?.OptionalArguments;
            VideoBitRate = presetBase?.VideoBitRate;
            VideoCodecOptions = presetBase?.VideoCodecOptions?.Select(x => new EncoderOptionValue(x)).ToHashSet();
            _videoEncoder = presetBase.VideoEncoder != null ? new Encoder(presetBase.VideoEncoder) : null;
        }

        public new List<FFmpegAudioStreamPreset> AudioStreamPresets { get; set; }

        public string ContainerExtension { get; set; }

        public bool Initialised { get; set; }

        public new HashSet<EncoderOptionValue> VideoCodecOptions { get; set; }

        public new Encoder VideoEncoder
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

        public override string ToString()
        {
            return string.Join(" | ", (new List<string>() { VideoEncoder?.Name, Name }).Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}