using Compressarr.JobProcessing.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegProcess
    {
        internal IConversion Converter;

        internal CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        internal bool cont = false;

        internal WorkItem WorkItem;

        public event EventHandler<string> OnUpdate;

        public bool Succeded;

        public string FileName;

        private List<JobEvent> _console = new();
        
        public IEnumerable<JobEvent> Console
        {
            get
            {
                return _console.ToList().OrderBy(e => e.Date);
            }

        }

        public void Output(string message, LogLevel level)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _console.Add(new JobEvent(level, message));
                Update();
            }
        }

        public void Update(object sender = null, string message = null)
        {

            OnUpdate?.Invoke(sender, message);
        }
    }
}