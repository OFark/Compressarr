using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Settings.FFmpegFactory
{
    public class FFmpegAudioStreamPresetBase
    {
        public FFmpegAudioStreamPresetBase()
        {

        }
        public FFmpegAudioStreamPresetBase(FFmpegAudioStreamPreset audioStreamPreset)
        {
            Filters = audioStreamPreset?.Filters?.Select(x => new FFmpegAudioStreamPresetFilterBase(x)).ToList();
            Action = audioStreamPreset?.Action ?? default;
            BitRate = audioStreamPreset?.BitRate;
            Encoder = audioStreamPreset.Encoder != null ? new(audioStreamPreset.Encoder) : null;
        }

        public List<FFmpegAudioStreamPresetFilterBase> Filters { get; set; }
        public AudioStreamAction Action { get; set; }
        public string BitRate { get; set; }
        public EncoderBase Encoder { get; set; }
    }
}
