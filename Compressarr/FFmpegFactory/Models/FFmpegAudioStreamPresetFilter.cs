using Compressarr.Filtering.Models;
using Compressarr.Settings.FFmpegFactory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegAudioStreamPresetFilter : FFmpegAudioStreamPresetFilterBase
    {
        public FFmpegAudioStreamPresetFilter()
        {

        }

        public FFmpegAudioStreamPresetFilter(FFmpegAudioStreamPresetFilterBase audioStreamPresetFilterBase)
        {
            ChannelValue = audioStreamPresetFilterBase?.ChannelValue ?? 0;
            Matches = audioStreamPresetFilterBase?.Matches ?? false;
            NumberComparitor = audioStreamPresetFilterBase.NumberComparitor != null ? new FilterComparitor(audioStreamPresetFilterBase.NumberComparitor.Value) : null;
            Rule = audioStreamPresetFilterBase?.Rule ?? default;
            Values = audioStreamPresetFilterBase?.Values;
        }

        public new FilterComparitor NumberComparitor { get;  set; }
    }
}
