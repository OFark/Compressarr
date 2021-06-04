using Compressarr.FFmpeg.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Compressarr.Settings.FFmpegFactory;
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

namespace Compressarr.Application
{
    public class ApplicationService : IApplicationService
    {
        private readonly SemaphoreSlim appSettingslock = new(1, 1);
        private readonly IConfiguration configuration;
        private readonly IFileService fileService;
        private readonly ILogger<ApplicationService> logger;
        public ApplicationService(IConfiguration configuration, IFileService fileService, ILogger<ApplicationService> logger, IOptions<AppSettings> appSettings, IOptions<APIServiceSettings> apiServiceSettings, IOptions<HashSet<FFmpegPresetBase>> presets, IOptions<HashSet<Filter>> filters, IOptions<HashSet<Job>> jobs, IHostApplicationLifetime lifetime)
        {
            this.configuration = configuration;
            this.fileService = fileService;

            AlwaysCalculateSSIM = appSettings?.Value?.AlwaysCalculateSSIM ?? false;
            ArgCalcSampleLength = appSettings?.Value?.ArgCalcSampleLength ?? new TimeSpan(0, 2, 0);
            AutoCalculationPost = appSettings?.Value?.AutoCalculationPost;
            AutoCalculationType = appSettings?.Value?.AutoCalculationType ?? AutoCalcType.BestGuess;
            CacheMediaInfo = appSettings?.Value?.CacheMediaInfo ?? false;
            InsertNamesIntoFFmpegPreviews = appSettings?.Value?.InsertNamesIntoFFmpegPreviews ?? false;

            Filters = filters?.Value ?? new();
            Jobs = jobs?.Value ?? new();
            Presets = presets?.Value.Select(p => new FFmpegPreset(p)).ToHashSet() ?? new();
            RadarrSettings = apiServiceSettings?.Value?.RadarrSettings ?? new();
            SonarrSettings = apiServiceSettings?.Value?.SonarrSettings ?? new();

            this.logger = logger;

            AppStoppingCancellationToken = lifetime.ApplicationStopping;

            InitialiseFFmpeg = new(() => { });
            InitialisePresets = new(() => { });

        }

        public event EventHandler<string> OnBroadcast;

        //App Settings
        public bool AlwaysCalculateSSIM { get; set; }
        public TimeSpan? ArgCalcSampleLength { get; set; }
        public decimal? AutoCalculationPost { get; set; }
        public AutoCalcType AutoCalculationType { get; set; }


        public bool CacheMediaInfo { get; set; }
        public CancellationToken AppStoppingCancellationToken { get; set; }
        public Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        public SortedSet<ContainerResponse> Containers { get; set; }
        public Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        public string FFmpegVersion { get; set; }
        public HashSet<Filter> Filters { get; set; }
        public SortedSet<string> HardwareDecoders { get; set; }

        public bool InsertNamesIntoFFmpegPreviews { get; set; }
        public HashSet<Job> Jobs { get; set; }
        public IEnumerable<Movie> Movies { get; set; }
        public HashSet<FFmpegPreset> Presets { get; set; }
        public double Progress { get; set; }
        public APISettings RadarrSettings { get; set; }
        public APISettings SonarrSettings { get; set; }
        public string State { get; set; }
        public Queue<string> StateHistory { get; set; } = new();
        public Task InitialiseFFmpeg { get; set; }
        public Task InitialisePresets { get; set; }

        public void Broadcast(string message) => OnBroadcast?.Invoke(this, message);
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
                jsonObj["Presets"] = JToken.FromObject(Presets.Select(x => new FFmpegPresetBase(x)));
                jsonObj["Jobs"] = JToken.FromObject(Jobs);
                jsonObj["Settings"] = JToken.FromObject(new AppSettings()
                {
                    AlwaysCalculateSSIM = AlwaysCalculateSSIM,
                    ArgCalcSampleLength = ArgCalcSampleLength ?? new TimeSpan(0, 2, 0),
                    AutoCalculationPost = AutoCalculationPost,
                    AutoCalculationType = AutoCalculationType,
                    CacheMediaInfo = CacheMediaInfo,
                    InsertNamesIntoFFmpegPreviews = InsertNamesIntoFFmpegPreviews
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

                    await appSettingslock.WaitAsync(AppStoppingCancellationToken);
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

                await appSettingslock.WaitAsync(AppStoppingCancellationToken);
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
