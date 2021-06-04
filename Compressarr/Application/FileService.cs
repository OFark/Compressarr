using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public enum AppDir
    {
        Cache,
        CodecOptions,
        Config,
        Debug,
        FFmpeg,
        Logs
    }

    public enum AppFile
    {
        ffmpegVersion,
        appsettings,
        mediaInfo
    }

    public class FileService : IFileService
    {
        private readonly CancellationToken cancellationToken;
        private readonly ILogger<FileService> logger;
        private Dictionary<string, SemaphoreSlim> locks = new();

        public FileService(ILogger<FileService> logger, IHostApplicationLifetime lifetime)
        {
            this.logger = logger;

            cancellationToken = lifetime?.ApplicationStopping ?? new();
            if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
        }

        public string ConfigDirectory => GetAppDirPath(AppDir.Config);

        public string FFMPEGPath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetFilePath(AppDir.FFmpeg, "ffmpeg.exe")
                                  : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GetFilePath(AppDir.FFmpeg, "ffmpeg")
                                  : throw new NotSupportedException("Cannot Identify OS");

        public string FFPROBEPath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetFilePath(AppDir.FFmpeg, "ffprobe.exe")
                                   : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GetFilePath(AppDir.FFmpeg, "ffprobe")
                                   : throw new NotSupportedException("Cannot Identify OS");

        public string TempDir => Path.Combine(Path.GetTempPath(), "Compressarr");

        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public Task DumpDebugFile(string fileName, string content) => WriteTextFileAsync(GetFilePath(AppDir.Debug, fileName), content);

        public string GetAppDirPath(AppDir dir) => dir switch
        {
            AppDir.Cache => Path.Combine(ConfigDirectory, "cache"),
            AppDir.CodecOptions => AppEnvironment.IsDevelopment ? "CodecOptions" : Path.Combine(ConfigDirectory, "CodecOptions"),
            AppDir.Config => AppEnvironment.InDocker ? "/config" : AppEnvironment.IsDevelopment ? "config" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config"),
            AppDir.Debug => Path.Combine(ConfigDirectory, "debug"),
            AppDir.FFmpeg => AppEnvironment.InNvidiaDocker ? "/usr/local/bin/" : Path.Combine(ConfigDirectory, "FFmpeg"),
            AppDir.Logs => Path.Combine(ConfigDirectory, "logs"),
            _ => Path.Combine(ConfigDirectory, dir.ToString())
        };

        public string GetAppFilePath(AppFile file) => file switch
        {
            AppFile.ffmpegVersion => Path.Combine(GetAppDirPath(AppDir.FFmpeg), "version.json"),
            AppFile.appsettings => AppEnvironment.InDocker ? Path.Combine(ConfigDirectory, $"{file.ToString().ToLower()}.json") : AppEnvironment.IsDevelopment ? $"{file.ToString().ToLower()}.Development.json" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{file.ToString().ToLower()}.json"),
            AppFile.mediaInfo => Path.Combine(GetAppDirPath(AppDir.Config), "mediaInfo.db"),
            _ => Path.Combine(ConfigDirectory, $"{file.ToString().ToLower()}.json")
        };

        public string GetFilePath(AppDir dir, string filename)
        {
            return Path.Combine(GetAppDirPath(dir), filename);
        }

        public bool HasFile(string filePath) => File.Exists(filePath);

        public bool HasFile(AppFile file) => File.Exists(GetAppFilePath(file));

        public bool HasFile(AppDir dir, string fileName) => File.Exists(Path.Combine(GetAppDirPath(dir), fileName));
        public Task<T> ReadJsonFileAsync<T>(AppFile file) where T : class => ReadJsonFileAsync<T>(GetAppFilePath(file));
        public Task<T> ReadJsonFileAsync<T>(AppDir dir, string fileName) where T : class => ReadJsonFileAsync<T>(GetFilePath(dir, fileName));

        public async Task<T> ReadJsonFileAsync<T>(string path) where T : class
        {
            using (logger.BeginScope("ReadJsonFileAsync"))
            {
                var json = await ReadTextFileAsync(path);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        logger.LogDebug($"Converting string to {typeof(T)}");
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                    catch (JsonSerializationException jsex)
                    {
                        logger.LogError($"JSON parsing error: {jsex}.");
                    }
                }
                else
                {
                    logger.LogWarning($"File empty: {path}.");
                }

                return default;
            }
        }

        public Task<string> ReadTextFileAsync(AppFile file) => ReadTextFileAsync(GetAppFilePath(file));

        public async Task<string> ReadTextFileAsync(string path)
        {
            using (logger.BeginScope("ReadTextFileAsync from {path}", path))
            {
                if (File.Exists(path))
                {

                    await GetLock(path).WaitAsync(cancellationToken);
                    try
                    {
                        logger.LogDebug($"Reading all text from file: {path}");
                        return await File.ReadAllTextAsync(path);
                    }
                    finally
                    {
                        GetLock(path).Release();
                    }
                }

                logger.LogWarning("File does not exist");
                return null;
            }
        }

        public Task WriteJsonFileAsync(AppDir dir, string fileName, object content) => WriteJsonFileAsync(GetFilePath(dir, fileName), content);
        public Task WriteJsonFileAsync(AppFile file, object content) => WriteJsonFileAsync(GetAppFilePath(file), content);
        public Task WriteJsonFileAsync(string path, object content) =>
            WriteTextFileAsync(path, JsonConvert.SerializeObject(content, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore }));

        public Task WriteTextFileAsync(AppDir dir, string fileName, string content) => WriteTextFileAsync(GetFilePath(dir, fileName), content);
        public Task WriteTextFileAsync(AppFile file, string content) => WriteTextFileAsync(GetAppFilePath(file), content);

        public async Task WriteTextFileAsync(string path, string content)
        {
            using (logger.BeginScope("WriteFileAsyncfrom {path}", path))
            {
                var fileDir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(fileDir) && !Directory.Exists(fileDir))
                {
                    logger.LogDebug($"Creating Directory: {fileDir}");
                    Directory.CreateDirectory(fileDir);
                }

                await GetLock(path).WaitAsync(cancellationToken);

                try
                {
                    logger.LogDebug($"Writing all text to file: {path}");
                    await File.WriteAllTextAsync(path, content);
                }
                finally
                {
                    GetLock(path).Release();
                }
            }
        }


        private SemaphoreSlim GetLock(string path)
        {
            logger.LogDebug($"Getting lock for: {path}");
            if (!locks.ContainsKey(path))
            {
                logger.LogTrace("Creating new Lock");
                locks.Add(path, new(1, 1));
            }
            return locks[path];
        }
    }
}
