using Compressarr.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Compressarr
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureDefaultFiles()
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    if (AppEnvironment.InDocker)
                    {
                        var fs = new FileService(null, null);
                        configBuilder.AddJsonFile(fs.GetAppFilePath(AppFile.appsettings), true, true);
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
