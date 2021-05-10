using Compressarr.JobProcessing.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegProcess
    {
        internal IConversion Converter;

        internal CancellationTokenSource cancellationTokenSource = new ();

        internal bool cont = false;

        internal WorkItem WorkItem;

        public event EventHandler<string> OnUpdate;

        public bool Succeded;

        public string FileName;
        
        public ImmutableSortedSet<JobEvent> Console { get; set; }

        public void Output(string message, LogLevel level)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console = (Console ?? ImmutableSortedSet.Create<JobEvent>()).Add(new JobEvent(level, message)).TakeLast(100).ToImmutableSortedSet();
                Update();
            }
        }

        public void Update(object sender = null, string message = null)
        {

            OnUpdate?.Invoke(sender, message);
        }
    }
}