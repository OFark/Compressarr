using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace Compressarr
{
    public static class WebHostExtensions
    {
        public static IHost InitFFMPEG(this IHost webHost)
        {
            var ffmpegManager = webHost.Services.GetService(typeof(IFFmpegManager)) as IFFmpegManager;

            ffmpegManager.Init();
            
            return webHost;
        }

        public static IHost InitJobs(this IHost webHost)
        {
            var jobManager = webHost.Services.GetService(typeof(IJobManager)) as IJobManager;

            jobManager.Init();

            return webHost;
        }
    }
}
