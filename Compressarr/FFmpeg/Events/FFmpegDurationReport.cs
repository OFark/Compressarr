using Compressarr.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg.Events
{
    public class FFmpegDurationReport
    {
        //Conversion:
        //"  Duration: 00:01:13.20, start: 0.000000, bitrate: 971 kb/s";
        //"  Duration: 00:01:13.24, start: 0.000000, bitrate: 379 kb/s"

        private static readonly Regex DurationReg = new(@"Duration: ([\d\.:]+)");

        private FFmpegDurationReport(Match match)
        {
            if (TimeSpan.TryParse(match.Groups[1].Value.Trim(), out var duration)) Duration = duration;
        }

        public TimeSpan Duration { get; set; }

        public static bool TryParse(string s, out FFmpegDurationReport result)
        {
            if (DurationReg.TryMatch(s, out var match))
            {
                result = new FFmpegDurationReport(match);
                return true;
            }
            result = null;
            return false;
        }
    }    
}
