using Compressarr.Filtering;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xabe.FFmpeg;

namespace Compressarr.JobProcessing.Models

{
    public class WorkItem
    {
        public IEnumerable<string> Arguments { get; set; }
        public string Bitrate { get; internal set; }
        public decimal? Compression { get; internal set; }
        public string DestinationFile { get; set; }
        public string DestinationFileName => Path.GetFileName(DestinationFile);
        public TimeSpan? Duration { get; internal set; }
        public bool Finished { get; internal set; } = false;
        public decimal? FPS { get; internal set; }
        public long? Frame { get; internal set; }
        public IMediaInfo MediaInfo { get; set; }
        public string MovieName { get; set; }
        public string Name => MovieName ?? SourceFileName;
        public int? Percent { get; internal set; }
        public decimal? Q { get; internal set; }
        public bool Running { get; internal set; } = false;
        public string Size { get; internal set; }
        public MediaSource Source { get; set; }
        public string SourceFile { get; set; }
        public string SourceFileName => Path.GetFileName(SourceFile);
        public int SourceID { get; set; }
        public string Speed { get; internal set; }
        public decimal? SSIM { get; set; }
        public bool Success { get; internal set; } = false;
        public TimeSpan? TotalLength { get; internal set; }
        public bool ShowArgs { get; set; }
    }
}