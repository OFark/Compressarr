using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public class ApplicationService : IApplicationService
    {
        private readonly CancellationToken cancellationToken;
        private readonly IConfiguration configuration;
        private readonly IFileService fileService;
        private readonly ILogger<ApplicationService> logger;
        private SemaphoreSlim appSettingslock = new(1,1);

        public ApplicationService(IConfiguration configuration, IFileService fileService, ILogger<ApplicationService> logger, IOptions<AppSettings> appSettings, IOptions<APIServiceSettings> apiServiceSettings, IOptions<HashSet<FFmpegPreset>> presets, IOptions<HashSet<Filter>> filters, IOptions<HashSet<Job>> jobs, IHostApplicationLifetime lifetime)
        {
            this.configuration = configuration;
            this.fileService = fileService;

            LoadMediaInfoOnFilters = appSettings?.Value?.LoadMediaInfoOnFilters ?? false;
            InsertNamesIntoFFmpegPreviews = appSettings?.Value?.InsertNamesIntoFFmpegPreviews ?? false;

            Filters = filters?.Value ?? new();
            Jobs = jobs?.Value ?? new();
            Presets = presets?.Value ?? new();
            RadarrSettings = apiServiceSettings?.Value?.RadarrSettings ?? new();
            SonarrSettings = apiServiceSettings?.Value?.SonarrSettings ?? new();

            this.logger = logger;

            cancellationToken = lifetime.ApplicationStopping;

            FFMpegReady = new();
            Initialised = new();
        }

        public string State { get; set; } = "Started";

        public AsyncManualResetEvent FFMpegReady { get; }
        public AsyncManualResetEvent Initialised { get; }

        public event EventHandler<string> OnBroadcast;
        public void Broadcast(string message) => OnBroadcast?.Invoke(this, message);

        public Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        public SortedDictionary<string, string> Containers { get; set; }
        public Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        public HashSet<Filter> Filters { get; set; }
        public HashSet<Job> Jobs { get; set; }
        public HashSet<FFmpegPreset> Presets { get; set; }
        public APISettings RadarrSettings { get; set; }
        public APISettings SonarrSettings { get; set; }
        public IEnumerable<Movie> Movies { get; set; }

        //App Settings
        public bool LoadMediaInfoOnFilters { get; set; }
        public bool InsertNamesIntoFFmpegPreviews { get; set; }


        public string FFmpegVersion { get; set; }


        public LogLevel GetLogLevel()
        {
            var logSettings = configuration.GetSection("Logging");
            return (LogLevel)Enum.Parse(typeof(LogLevel), logSettings["LogLevel:Default"]);
        }

        public async Task SaveAppSetting()
        {
            try
            {
                var jsonObj = await ReadAppSettings<dynamic>();

                var serviceSettings = new APIServiceSettings() { RadarrSettings = RadarrSettings, SonarrSettings = SonarrSettings };
                jsonObj["Services"] = JToken.FromObject(serviceSettings);
                jsonObj["Filters"] = JToken.FromObject(Filters);
                jsonObj["Presets"] = JToken.FromObject(Presets);
                jsonObj["Jobs"] = JToken.FromObject(Jobs);                
                jsonObj["Settings"] = JToken.FromObject(new AppSettings()
                {
                    InsertNamesIntoFFmpegPreviews = InsertNamesIntoFFmpegPreviews,
                    LoadMediaInfoOnFilters = LoadMediaInfoOnFilters
                });

                await WriteAppSettings(jsonObj);

            }
            catch (ConfigurationErrorsException)
            {
                logger.LogError("Error writing app settings");
            }
        }

        public async Task UpdateLogLevel(LogLevel level)
        {
            var jsonObj = await ReadAppSettings<dynamic>();
            jsonObj["Logging"]["LogLevel"]["Default"] = level.ToString();

            await WriteAppSettings(jsonObj);
        }

        private async Task<T> ReadAppSettings<T>() where T : class
        {
            using (logger.BeginScope("ReadAppSettings"))
            {
                var path = fileService.GetAppFilePath(AppFile.appsettings);

                if (File.Exists(path))
                {
                    logger.LogDebug($"Reading all text from file: {path}");

                    string json = null;

                    await appSettingslock.WaitAsync(cancellationToken);
                    try
                    {
                        logger.LogDebug($"Writing all text to file: {path}");
                        json = await File.ReadAllTextAsync(path);
                    }
                    finally
                    {
                        appSettingslock.Release();
                    }

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
                }

                logger.LogWarning("File does not exist");

                return default;
            }
        }
        private async Task WriteAppSettings(object settings)
        {
            var path = fileService.GetAppFilePath(AppFile.appsettings);

            using (logger.BeginScope("WriteFileAsyncfrom {path}", path))
            {
                var content = JsonConvert.SerializeObject(settings, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore });

                await appSettingslock.WaitAsync(cancellationToken);
                try
                {
                    logger.LogDebug($"Writing all text to file: {path}");
                    await File.WriteAllTextAsync(path, content);
                }
                finally
                {
                    appSettingslock.Release();
                }
            }
        }
    }
}
