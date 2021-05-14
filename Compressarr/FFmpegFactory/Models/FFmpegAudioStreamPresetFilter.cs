using Compressarr.Filtering.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegAudioStreamPresetFilter
    {
        private FilterComparitor numberComparitor;

        public int ChannelValue { get; set; }
        public bool Matches { get; set; }


        public FilterComparitor NumberComparitor { get;  set; }
        public AudioStreamRule Rule { get; set; }
        public HashSet<string> Values { get; set; }
    }
}
