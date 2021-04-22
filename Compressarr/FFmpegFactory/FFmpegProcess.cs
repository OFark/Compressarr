using Compressarr.JobProcessing.Models;
using System;
using System.Threading;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory
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

        public void Update(object sender = null, string message = null)
        {

            OnUpdate?.Invoke(sender, message);
        }
    }
}