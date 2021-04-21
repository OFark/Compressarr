using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing;
using Compressarr.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

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

        public static IHostBuilder ConfigureDefaultFiles(this IHostBuilder builder)
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != Environments.Development)
            {
                if (!Directory.Exists(SettingsManager.ConfigDirectory)) Directory.CreateDirectory(SettingsManager.ConfigDirectory);
                if (!Directory.Exists(SettingsManager.CodecOptionsDirectory)) Directory.CreateDirectory(SettingsManager.CodecOptionsDirectory);

                if (SettingsManager.InDocker && !File.Exists(SettingsManager.dockerAppSettings))
                {
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Docker.json"), SettingsManager.dockerAppSettings);
                }

                foreach (var f in new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodecOptions")).GetFiles())
                {
                    if (!File.Exists(Path.Combine(SettingsManager.CodecOptionsDirectory, f.Name)))
                    {
                        f.CopyTo(Path.Combine(SettingsManager.CodecOptionsDirectory, f.Name));
                    }
                }

            }
            return builder;
        }
    }
}
