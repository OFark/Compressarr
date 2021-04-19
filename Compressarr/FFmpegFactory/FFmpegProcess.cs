using Compressarr.JobProcessing;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegProcess
    {
        internal IConversion Converter;

        internal CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        internal bool cont = false;

        internal WorkItem WorkItem;

        public event EventHandler OnUpdate;

        public bool Succeded;

        public string FileName;

        public void Update(object sender = null)
        {
            OnUpdate?.Invoke(sender, EventArgs.Empty);
        }
    }
}