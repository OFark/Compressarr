using System;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory
{
    public interface IFFmpegInitialiser
    {
        bool Ready { get; }
        string Version { get; }

        event EventHandler OnReady;
        event EventHandler<string> OnBroadcast;

        Task Start();
    }
}