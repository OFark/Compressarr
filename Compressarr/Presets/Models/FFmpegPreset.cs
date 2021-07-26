using Compressarr.Helpers;
using Compressarr.Settings.FFmpegFactory;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Compressarr.Presets.Models
{
    public class FFmpegPreset : FFmpegPresetBase
    {
        private Encoder _videoEncoder;
        private Encoder _subtitleEncoder;

        public FFmpegPreset()
        {
            AudioStreamPresets ??= new() { new() };
        }


        public FFmpegPreset(FFmpegPresetBase presetBase)
        {
            AudioStreamPresets = presetBase?.AudioStreamPresets?.Select(x => new FFmpegAudioStreamPreset(x)).ToList();
            B_Frames = presetBase.B_Frames;
            Container = presetBase?.Container;
            CopyAttachments = presetBase.CopyAttachments;
            CopyData = presetBase.CopyData;
            CopyMetadata = presetBase.CopyMetadata;
            CopySubtitles = presetBase.CopySubtitles;
            FrameRate = presetBase?.FrameRate;
            HardwareDecoder = presetBase?.HardwareDecoder;
            Name = presetBase?.Name;
            OptionalArguments = presetBase?.OptionalArguments;
            _subtitleEncoder = presetBase.SubtitleEncoder != null ? new Encoder(presetBase.SubtitleEncoder) : null;
            VideoBitRate = presetBase?.VideoBitRate;
            VideoBitRateAutoCalc = presetBase.VideoBitRateAutoCalc;
            VideoEncoderOptions = presetBase?.VideoEncoderOptions?.Select(x => new EncoderOptionValue(x)).ToHashSet();
            _videoEncoder = presetBase.VideoEncoder != null ? new Encoder(presetBase.VideoEncoder) : null;            
        }

        public new List<FFmpegAudioStreamPreset> AudioStreamPresets { get; set; }
        public string ContainerExtension { get; set; }
        public bool Initialised { get; set; }

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
                    VideoEncoderOptions = value?.Options?.WithValues();
                }
                else
                {
                    VideoEncoderOptions = value?.Options?.WithValues(VideoEncoderOptions);
                }

                _videoEncoder = value;

            }
        }

        public new Encoder SubtitleEncoder
        {
            get
            {
                return _subtitleEncoder ?? new();
            }
            set
            {
                _subtitleEncoder = value;

            }
        }

        public new HashSet<EncoderOptionValue> VideoEncoderOptions { get; set; }
        public override string ToString()
        {
            return string.Join(" | ", (new List<string>() { VideoEncoder?.Name, Name }).Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}