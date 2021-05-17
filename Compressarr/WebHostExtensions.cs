using Compressarr.Application;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

namespace Compressarr
{
    public static class WebHostExtensions
    {
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
