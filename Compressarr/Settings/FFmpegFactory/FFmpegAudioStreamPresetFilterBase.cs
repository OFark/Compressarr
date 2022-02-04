using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Settings.Filtering;
using System.Collections.Generic;

namespace Compressarr.Settings.FFmpegFactory
{
    public class FFmpegAudioStreamPresetFilterBase
    {
        public FFmpegAudioStreamPresetFilterBase()
        {

        }
        public FFmpegAudioStreamPresetFilterBase(FFmpegAudioStreamPresetFilter aspf)
        {
            ChannelValue = aspf?.ChannelValue ?? 0;
            Matches = aspf?.Matches ?? false;
            NumberComparitor = aspf?.NumberComparitor?.Value != null ? new() { Value = aspf.NumberComparitor.Value } : null;
            Rule = aspf?.Rule ?? default;
            Values = aspf?.Values;
        }
        public int ChannelValue { get; set; }
        public bool Matches { get; set; }

        public FilterComparitorBase NumberComparitor { get;  set; }
        public AudioStreamRule? Rule { get; set; }
        public IEnumerable<string> Values { get; set; }
    }
}
