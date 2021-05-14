using Compressarr.Application;
using Compressarr.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr
{
    public static class WebHostExtensions
    {
        public static IServiceCollection AddStartupTask<T>(this IServiceCollection services) where T : class, IStartupTask
            => services.AddTransient<IStartupTask, T>();

        public static async Task RunWithTasksAsync(this IHost webHost, CancellationToken cancellationToken = default)
        {
            // Load all tasks from DI
            var startupTasks = webHost.Services.GetServices<IStartupTask>();

            // Execute all the tasks
            foreach (var startupTask in startupTasks)
            {
                _ = startupTask.ExecuteAsync(cancellationToken);
            }

            // Start the tasks as normal
            await webHost.RunAsync(cancellationToken);
        }

        public static IHostBuilder ConfigureDefaultFiles(this IHostBuilder builder)
        {
            if (!AppEnvironment.IsDevelopment)
            {
                var fs = new FileService(null, null);

                if (!Directory.Exists(fs.ConfigDirectory)) Directory.CreateDirectory(fs.ConfigDirectory);
                if (!Directory.Exists(fs.GetAppDirPath(AppDir.CodecOptions))) Directory.CreateDirectory(fs.GetAppDirPath(AppDir.CodecOptions));

                if (AppEnvironment.InDocker && !File.Exists(fs.GetAppFilePath(AppFile.appsettings)))
                {
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Docker.json"), fs.GetAppFilePath(AppFile.appsettings));
                }

                foreach (var f in new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodecOptions")).GetFiles())
                {
                    if (!File.Exists(Path.Combine(fs.GetAppDirPath(AppDir.CodecOptions), f.Name)))
                    {
                        f.CopyTo(Path.Combine(fs.GetAppDirPath(AppDir.CodecOptions), f.Name));
                    }
                }

            }
            return builder;
        }
    }
}
