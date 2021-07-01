using Compressarr.Helpers;
using System;
using System.Text.RegularExpressions;

namespace Compressarr.FFmpeg.Events
{

    public delegate void FFmpegSSIMReportEvent(FFmpegSSIMReport args);

    public class FFmpegSSIMReport : EventArgs
    {
        //SSIM:
        //"frame=16622 fps=2073 q=-0.0 size=N/A time=00:11:06.93 bitrate=N/A speed=83.2x"
        //"[Parsed_ssim_4 @ 000002aaab376c00] SSIM Y:0.995682 (23.646920) U:0.995202 (23.189259) V:0.995373 (23.347260) All:0.995550 (23.516743)"
        //"[Parsed_ssim_4 @ 000001d1588cd080] SSIM Y:1.000000 (inf) U:1.000000 (inf) V:1.000000 (inf) All:1.000000 (inf)"

        private FFmpegSSIMReport(Match match)
        {
            if (decimal.TryParse(match.Groups[1].Captures[3].Value.Split(":")[1].Trim(), out var ssim)) { SSIM = ssim; }
        }

        public static Regex SSIMReg { get => new(@"^\[Parsed_ssim_4.* SSIM(?: (\w+:\d+\.\d+) \([\w\d\.]+\))+"); }

        public decimal SSIM { get; set; }
        public static bool TryParse(string s, out FFmpegSSIMReport result)
        {
            if (!string.IsNullOrWhiteSpace(s) && SSIMReg.TryMatch(s, out var match))
            {
                result = new FFmpegSSIMReport(match);
                return true;
            }
            result = null;
            return false;
        }
    }
}

