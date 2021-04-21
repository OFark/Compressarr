using Compressarr.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using System;
using System.IO;

namespace Compressarr
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build()
                                       .InitFFMPEG()
                                       .InitJobs()
                                       .Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureDefaultFiles()
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    if (SettingsManager.InDocker)
                    {
                        configBuilder.AddJsonFile(SettingsManager.dockerAppSettings);
                    }
                })
                .ConfigureLogging((hostBuilder, loggingBuilder) =>
                {
                    loggingBuilder
                        .AddFile(hostBuilder.Configuration.GetSection("Logging:File"));
                });
    }
}
