using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing;
using Microsoft.Extensions.Hosting;
using System;

namespace Compressarr
{
    public static class WebHostExtensions
    {
        public static IHost InitFFMPEG(this IHost webHost)
        {
            Console.WriteLine("Initialising FFMPEG, which means getting the latest version");
            var ffmpegManager = webHost.Services.GetService(typeof(IFFmpegManager)) as IFFmpegManager;

            ffmpegManager.Init();
            
            return webHost;
        }

        public static IHost InitJobs(this IHost webHost)
        {
            Console.WriteLine("Initialising Jobs");
            var jobManager = webHost.Services.GetService(typeof(IJobManager)) as IJobManager;

            jobManager.Init();

            return webHost;
        }
    }
}
