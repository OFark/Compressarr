using Compressarr.FFmpegFactory.Interfaces;
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
    }
}
