using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory
{
    public abstract class FFmpegReliant
    {
        public bool Ready { get; set; }
        public EventWaitHandle ReadyEvent { get; } = new AutoResetEvent(false);
        protected Action RunOnReady { get; set; }

        public void WhenReady(IFFmpegInitialiser fFmpegInitialiser, Action runOnReady)
        {
            RunOnReady = runOnReady;

            Ready = fFmpegInitialiser.Ready;
            if (!Ready)
            {
                fFmpegInitialiser.OnReady += FFmpegInitialiser_OnReady;
            }
            else
            {
                RunOnReady?.Invoke();
            }
        }

        private void FFmpegInitialiser_OnReady(object sender, EventArgs e)
        {
            Ready = true;
            RunOnReady?.Invoke();
            ReadyEvent.Set();
        }
    }
}
