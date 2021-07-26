using ByteSizeLib;
using Compressarr.Helpers;
using System;
using System.Text.RegularExpressions;

namespace Compressarr.FFmpeg.Events
{
    public delegate void FFmpegProgressEvent(FFmpegProgress args);

    public delegate void FFmpegStdOutEvent(string data);

    public class FFmpegProgress : EventArgs
    {
        //Conversion:
        //"frame= 2171 fps= 58 q=-0.0 size=    4396kB time=00:01:28.50 bitrate= 406.9kbits/s speed=2.38x    ";
        //"frame= 3097 fps= 22 q=-0.0 size=N/A time=00:02:04.72 bitrate=N/A speed=0.886x"
        //"frame=   81 fps=0.0 q=-0.0 size=N/A time=00:00:03.44 bitrate=N/A speed=6.69x"

        private static readonly Regex ProgressReg = new(@"frame= *(\d*) fps= *([\d\.]*) q=*(-?[\d\.]*)(?: q=*-?[\d\.]*)* size= *([^ ]*) time= *([\d:\.]*) bitrate= *([^ ]*) speed= *([\d.]*x) *");

        private FFmpegProgress(Match match)
        {
            if (long.TryParse(match.Groups[1].Value.Trim(), out var frame)) Frame = frame;
            if (decimal.TryParse(match.Groups[2].Value.Trim(), out var fps)) FPS = fps;
            if (decimal.TryParse(match.Groups[3].Value.Trim(), out var q)) Q = q;
            if (ByteSize.TryParse(match.Groups[4].Value.Trim(), out var size)) Size = size.Bytes;
            if (TimeSpan.TryParse(match.Groups[5].Value.Trim(), out var time)) Time = time;
            Bitrate = match.Groups[6].Value.Trim();
            if (decimal.TryParse(match.Groups[7].Value.Trim().Replace("x", ""), out var speed)) Speed = speed;
        }

        public FFmpegProgress CalculatePercentage(TimeSpan duration)
        {
            if(duration != default)
            {
                Percentage = (int)((decimal)Time.Ticks / duration.Ticks * 100);
            }

            return this;
        }

        public string Bitrate { get; init; }
        public decimal FPS { get; init; }
        public long Frame { get; init; }
        public decimal Q { get; init; }
        public double Size { get; init; }
        public decimal Speed { get; init; }
        public TimeSpan Time { get; init; }

        public string StdOut { get; set; }

        public int Percentage { get; set; }

        public static bool TryParse(string s, out FFmpegProgress result)
        {
            if (ProgressReg.TryMatch(s, out var match))
            {
                result = new FFmpegProgress(match);
                return true;
            }
            result = null;
            return false;
        }
    }
}
