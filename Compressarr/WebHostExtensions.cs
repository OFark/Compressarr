using Compressarr.FFmpegFactory;
using Compressarr.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
