using Compressarr.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compressarr
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build()
                                       .Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureDefaultFiles()
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    if (AppEnvironment.InDocker)
                    {
                        configBuilder.AddJsonFile(SettingsManager.GetAppFilePath(AppFile.appsettings), true, true);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging((hostBuilder, loggingBuilder) =>
                {
                    loggingBuilder
                        .AddFile(hostBuilder.Configuration.GetSection("Logging:File"));
                });
    }
}
