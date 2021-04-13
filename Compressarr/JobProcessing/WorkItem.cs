using Compressarr.Filtering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Compressarr.JobProcessing
{
    public class WorkItem
    {
        private List<string> _events = new List<string>();
        private object eventLock = new object();

        public List<string> Arguments { get; set; }
        public string Bitrate { get; internal set; }
        public string DestinationFile { get; set; }
        public TimeSpan? Duration { get; internal set; }

        public List<string> Events
        {
            get
            {
                lock (eventLock)
                {
                    return _events.ToList();
                }
            }
        }

        public bool Finished { get; internal set; } = false;
        public int? FPS { get; internal set; }
        public long? Frame { get; internal set; }
        public int? Percent { get; internal set; }
        public decimal? Q { get; internal set; }
        public bool Running { get; internal set; } = false;
        public string Size { get; internal set; }
        public MediaSource Source { get; set; }
        public string SourceFile { get; set; }
        public string SourceFileName => Path.GetFileName(SourceFile);
        public int SourceID { get; set; }
        public string Speed { get; internal set; }
        public bool Success { get; internal set; } = false;
        public TimeSpan? TotalLength { get; internal set; }

        public void UpdateStatus(params string[] messages)
        {
            lock (eventLock)
            {
                _events.AddRange(messages.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
        }
    }
}