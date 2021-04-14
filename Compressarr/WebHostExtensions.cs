using Compressarr.FFmpegFactory;
using Microsoft.Extensions.Hosting;
using System;

namespace Compressarr
{
    public static class WebHostExtensions
    {
        public static IHost InitFFMPEG(this IHost webHost)
        {
            Console.WriteLine("Initialising FFMPEG, which means getting the latest version");
            var ffmpegManager = webHost.Services.GetService(typeof(FFmpegManager)) as FFmpegManager;

            ffmpegManager.Init();
            
            return webHost;
        }
    }
}
