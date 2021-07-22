using Compressarr.Helpers;
using System;

namespace Compressarr.FFmpeg.Models
{
    public class FFmpegFormat : IComparable<FFmpegFormat>
    {
        public bool Demuxer { get; set; }
        public string Description { get; set; }
        public bool Muxer { get; set; }
        public string Name { get; set; }
        public int CompareTo(FFmpegFormat other)
        {
            return Name.CompareTo(other.Name);
        }

        public override string ToString() => " - ".JoinWithIfNotNull(Name, Description);
    }
}
