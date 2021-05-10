using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
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
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public enum AppDir
    {
        CodecOptions,
        Config,
        Debug,
        FFmpeg,
        Logs
    }

    public enum AppFile
    {
        ffmpegVersion,
        appsettings
    }

    public class SettingsManager : ISettingsManager
    {
        private readonly CancellationToken cancellationToken;
        private readonly ILogger<SettingsManager> logger;
        private readonly IConfiguration configuration;

        private Dictionary<string, SemaphoreSlim> locks = new();

        public SettingsManager(IConfiguration configuration, ILogger<SettingsManager> logger, IOptions<APIServiceSettings> appSettings, IOptions<HashSet<FFmpegPreset>> presets, IOptions<HashSet<Filter>> filters, IOptions<HashSet<Job>> jobs, IHostApplicationLifetime lifetime)
        {
            this.configuration = configuration;
            Filters = filters?.Value ?? new();
            Jobs = jobs?.Value ?? new();
            Presets = presets?.Value ?? new();
            RadarrSettings = appSettings?.Value?.RadarrSettings ?? new();
            SonarrSettings = appSettings?.Value?.SonarrSettings ?? new();

            this.logger = logger;

            cancellationToken = lifetime.ApplicationStopping;

            foreach (var ad in Enum.GetValues(typeof(AppDir)).Cast<AppDir>())
            {
                logger.LogDebug($"Application Directory ({ad}) set to: {GetAppDirPath(ad)}");
            }
        }

        public static string ConfigDirectory => GetAppDirPath(AppDir.Config);
        public Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        public SortedDictionary<string, string> Containers { get; set; }

        public HashSet<Filter> Filters { get; set; }
        public HashSet<Job> Jobs { get; set; }
        public HashSet<FFmpegPreset> Presets { get; set; }
        public APISettings RadarrSettings { get; set; }
        public APISettings SonarrSettings { get; set; }

        public static string GetAppDirPath(AppDir dir) => dir switch
        {
            AppDir.CodecOptions => AppEnvironment.IsDevelopment ? "CodecOptions" : Path.Combine(ConfigDirectory, "CodecOptions"),
            AppDir.Config => AppEnvironment.InDocker ? "/config" : AppEnvironment.IsDevelopment ? "config" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config"),
            AppDir.Debug => Path.Combine(ConfigDirectory, "debug"),
            AppDir.FFmpeg => Path.Combine(ConfigDirectory, "FFmpeg"),
            AppDir.Logs => Path.Combine(ConfigDirectory, "logs"),
            _ => Path.Combine(ConfigDirectory, dir.ToString())
        };

        public static string GetAppFilePath(AppFile file) => file switch
        {
            AppFile.ffmpegVersion => Path.Combine(GetAppDirPath(AppDir.FFmpeg), "version.json"),
            AppFile.appsettings => AppEnvironment.InDocker ? Path.Combine(ConfigDirectory, $"{file.ToString().ToLower()}.json") : AppEnvironment.IsDevelopment ? $"{file.ToString().ToLower()}.json" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{file.ToString().ToLower()}.json"),
            _ => Path.Combine(ConfigDirectory, $"{file.ToString().ToLower()}.json")
        };

        public static string GetFilePath(AppDir dir, string filename)
        {
            return Path.Combine(GetAppDirPath(dir), filename);
        }

        public static bool HasFile(string filePath) => File.Exists(filePath);

        public static bool HasFile(AppFile file) => File.Exists(GetAppFilePath(file));

        public static bool HasFile(AppDir dir, string fileName) => File.Exists(Path.Combine(GetAppDirPath(dir), fileName));

        public Task DumpDebugFile(string fileName, string content) => WriteTextFileAsync(Path.Combine(GetAppDirPath(AppDir.Debug), fileName), content);

        public Task<T> ReadJsonFileAsync<T>(AppFile file) where T : class => ReadJsonFileAsync<T>(GetAppFilePath(file));

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

        public async Task SaveAppSetting()
        {
            try
            {
                var jsonObj = await ReadJsonFileAsync<dynamic>(AppFile.appsettings);

                var serviceSettings = new APIServiceSettings() { RadarrSettings = RadarrSettings, SonarrSettings = SonarrSettings };
                jsonObj["Services"] = JToken.FromObject(serviceSettings);
                jsonObj["Filters"] = JToken.FromObject(Filters);
                jsonObj["Presets"] = JToken.FromObject(Presets);
                jsonObj["Jobs"] = JToken.FromObject(Jobs);

                await WriteJsonFileAsync(AppFile.appsettings, jsonObj);

            }
            catch (ConfigurationErrorsException)
            {
                logger.LogError("Error writing app settings");
            }
        }

        public LogLevel GetLogLevel()
        {
            var logSettings = configuration.GetSection("Logging");
            return (LogLevel)Enum.Parse(typeof(LogLevel), logSettings["LogLevel:Default"]);
        }

        public async Task UpdateLogLevel(LogLevel level)
        {
            var jsonObj = await ReadJsonFileAsync<dynamic>(AppFile.appsettings);
            var logging = jsonObj["Logging"];
            jsonObj["Logging"]["LogLevel"]["Default"] = level.ToString();

            await WriteJsonFileAsync(AppFile.appsettings, jsonObj);
        }

        public Task WriteJsonFileAsync(AppFile file, object content) => WriteJsonFileAsync(GetAppFilePath(file), content);
        public Task WriteJsonFileAsync(string path, object content) =>
            WriteTextFileAsync(path, JsonConvert.SerializeObject(content, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore }));


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
