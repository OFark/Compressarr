using Compressarr.Settings;
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
                if (!Directory.Exists(SettingsManager.ConfigDirectory)) Directory.CreateDirectory(SettingsManager.ConfigDirectory);
                if (!Directory.Exists(SettingsManager.GetAppDirPath(AppDir.CodecOptions))) Directory.CreateDirectory(SettingsManager.GetAppDirPath(AppDir.CodecOptions));

                if (AppEnvironment.InDocker && !File.Exists(SettingsManager.GetAppFilePath(AppFile.appsettings)))
                {
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Docker.json"), SettingsManager.GetAppFilePath(AppFile.appsettings));
                }

                foreach (var f in new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodecOptions")).GetFiles())
                {
                    if (!File.Exists(Path.Combine(SettingsManager.GetAppDirPath(AppDir.CodecOptions), f.Name)))
                    {
                        f.CopyTo(Path.Combine(SettingsManager.GetAppDirPath(AppDir.CodecOptions), f.Name));
                    }
                }

            }
            return builder;
        }
    }
}
