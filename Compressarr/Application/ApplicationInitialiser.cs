using Compressarr.Application.Models;
using Compressarr.FFmpeg;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.Application
{
    public class ApplicationInitialiser : IApplicationInitialiser
    {

        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly IFilterManager filterManager;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "This may come in handy")]
        private readonly Task InitialisationTask;

        private readonly IJobManager jobManager;
        private readonly ILogger<ApplicationInitialiser> logger;


        public ApplicationInitialiser(IApplicationService applicationService, IFFmpegProcessor fFmpegProcessor, IFileService fileService, IFilterManager filterManager, IJobManager jobManager, ILogger<ApplicationInitialiser> logger)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.filterManager = filterManager;
            this.jobManager = jobManager;
            this.logger = logger;

            applicationService.InitialisationSteps = new();
        }

        public event EventHandler<Update> OnUpdate;
        public event EventHandler OnComplete;

        public async Task InitialiseAsync()
        {
            using (logger.BeginScope("Initialising Application"))
            {
                try
                {
                    ApplyDatabaseTransforms();

                    ApplyFilterTransforms();

                    applicationService.InitialiseFFmpeg = InitialiseFFmpeg();

                    await applicationService.InitialiseFFmpeg;

                    applicationService.InitialisePresets = InitialisePresets();

                    await applicationService.InitialisePresets;

                    OnComplete?.Invoke(this, null);

                    logger.LogInformation("Application Initialisation complete");

                    if (applicationService.Jobs != null)
                    {
                        logger.LogInformation("Initialising Jobs");

                        foreach (var job in applicationService.Jobs.Where(j => !j.Initialised))
                        {
                            await jobManager.InitialiseJob(job, applicationService.AppStoppingCancellationToken);
                        }
                        logger.LogInformation("Job Initialisation complete");
                    }


                    logger.LogInformation("Ready");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Initialisation Error");
                }
            }
        }

        private void ApplyFilterTransforms()
        {
            foreach(var filter in applicationService.Filters)
            {
                foreach(var fil in filter.Filters)
                {
                    fil.Property.Value = (filter.MediaSource switch
                    {
                        MediaSource.Radarr => filterManager.RadarrFilterProperties,
                        MediaSource.Sonarr => filterManager.SonarrFilterProperties,
                        _ => throw new NotImplementedException()
                    }).FirstOrDefault(x => x.Key == fil.Property.Key).Value;
                    
                }
            }
        }

        private void ApplyDatabaseTransforms()
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var oldHistories = db.GetCollection<JobProcessing.Models.History>();

            var newHistories = db.GetCollection<History.Models.MediaHistory>();

            if (oldHistories != null && oldHistories.Count() > 0)
            {
                foreach (var his in oldHistories.FindAll())
                {
                    if (his.Entries != null)
                    {
                        foreach (var ent in his.Entries.Where(x => !string.IsNullOrWhiteSpace(x.FilePath)))
                        {
                            var history = newHistories.Include(x => x.Entries).Query().Where(x => x.FilePath == ent.FilePath).FirstOrDefault();
                            if (history == null)
                            {
                                history = new()
                                {
                                    FilePath = ent.FilePath
                                };

                                newHistories.Insert(history);
                            }

                            var entry = new History.Models.HistoryEntry()
                            {
                                Started = ent.Started,
                                Finished = ent.Finished,
                                HistoryID = ent.HistoryID,
                                Type = ent.Type
                            };

                            var processingHistory = new History.Models.ProcessingHistory()
                            {
                                Arguments = ent.Arguments,
                                Compression = ent.Compression,
                                DestinationFilePath = ent.FilePath,
                                FilterID = ent.FilterID,
                                FPS = ent.FPS,
                                Percentage = ent.Percentage,
                                Preset = ent.Preset,
                                Speed = ent.Speed,
                                SSIM = ent.SSIM,
                                Success = ent.Success
                            };

                            history.Entries ??= new();

                            entry.ProcessingHistory = processingHistory;
                            history.Entries.Add(entry);

                            newHistories.Update(history);
                        }
                    }
                }

                newHistories.EnsureIndex(x => x.Id);

                db.DropCollection("History");
            }

        }

        private async Task<string> GetFFmpegVersionAsync()
        {
            using (logger.BeginScope("Get FFmpeg Version."))
            {
                var result = await fFmpegProcessor.GetFFmpegVersionAsync(applicationService.AppStoppingCancellationToken);

                if (result.Success)
                    return result.Result;

                return null;
            }
        }

        private async Task<HashSet<EncoderOption>> GetOptionsAsync(string codec)
        {
            using (logger.BeginScope("Get Codec Options"))
            {
                var optionsFile = fileService.GetFilePath(AppDir.CodecOptions, $"{codec}.json");

                if (fileService.HasFile(optionsFile))
                {
                    return await fileService.ReadJsonFileAsync<HashSet<EncoderOption>>(optionsFile);
                }
                else
                {
                    logger.LogTrace($"Codec Options file not found.");
                }
                return null;
            }
        }

        private async Task InitialiseFFmpeg()
        {
            using (logger.BeginScope("Initialising FFmpeg"))
            {
                string existingVersion = null;

                var downloadStep = new InitialisationTask("Download FFmpeg");
                applicationService.InitialisationSteps.Add(downloadStep);

                using (var downloadWorker = new JobWorker(downloadStep.Condition, OnUpdate))
                {
                    if (!AppEnvironment.InNvidiaDocker)
                    {
                        logger.LogDebug("Not Nvidia Docker, checking for existing FFmpeg.");

                        var ffmpegAlreadyExists = fileService.HasFile(fileService.FFMPEGPath);
                        if (ffmpegAlreadyExists)
                        {
                            downloadStep.Name = "Updating FFmpeg";
                            existingVersion = await GetFFmpegVersionAsync();
                        }

                        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, fileService.GetAppDirPath(AppDir.FFmpeg),
                            new Progress<ProgressInfo>((info) =>
                            {
                                downloadStep.State = $"Downloading: {((decimal)info.DownloadedBytes / info.TotalBytes).ToPercent().Adorn("%")}";
                                OnUpdate?.Invoke(this, new());
                            }
                        ));

                        logger.LogDebug("FFmpeg latest version check finished.");

                        if (!fileService.HasFile(fileService.FFMPEGPath))
                        {
                            downloadWorker.Succeed(false);
                            SetState(downloadStep, "FFmpeg not found, download must have failed.");

                            return;
                        }

                        downloadWorker.Succeed();

                        if (!ffmpegAlreadyExists)
                        {
                            SetState(downloadStep, "FFmpeg finished Downloading");
                        }
                        else
                        {
                            applicationService.FFmpegVersion = await GetFFmpegVersionAsync();

                            if (existingVersion != applicationService.FFmpegVersion && applicationService.FFmpegVersion != null)
                            {
                                SetState(downloadStep, $"FFmpeg updated to: {applicationService.FFmpegVersion}");
                            }
                            else
                            {
                                SetState(downloadStep, "FFmpeg already up to date");
                            }
                        }

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            var chmodStep = new InitialisationTask("CHMOD FFmpeg");
                            applicationService.InitialisationSteps.Add(chmodStep);
                            using var chmodWorker = new JobWorker(chmodStep.Condition, OnUpdate);

                            logger.LogDebug("Running on Linux, CHMOD required");
                            foreach (var exe in new string[] { fileService.FFMPEGPath, fileService.FFPROBEPath })
                            {
                                await fFmpegProcessor.RunProcess("/bin/bash", $"-c \"chmod +x {exe}\"", applicationService.AppStoppingCancellationToken);
                            }
                            chmodWorker.Succeed();
                        }
                    }
                    else
                    {
                        applicationService.FFmpegVersion = await GetFFmpegVersionAsync();
                        SetState(downloadStep, $"Nvidia Docker, download not required. Existing Version: {applicationService.FFmpegVersion}");
                    }
                }

                var codecStep = new InitialisationTask("Get available codecs");
                applicationService.InitialisationSteps.Add(codecStep);
                using (var codecWorker = new JobWorker(codecStep.Condition, OnUpdate))
                {
                    logger.LogDebug($"Get available codecs.");

                    var codecs = new Dictionary<CodecType, SortedSet<Codec>>
                        {
                            { CodecType.Audio, new() },
                            { CodecType.Subtitle, new() },
                            { CodecType.Video, new() }
                        };

                    var result = await fFmpegProcessor.GetAvailableCodecsAsync(applicationService.AppStoppingCancellationToken);

                    if (result.Success)
                    {
                        foreach (var c in result.Results)
                        {
                            codecs[c.Type].Add(new(c.Name, c.Description, c.IsDecoder, c.IsEncoder));
                        }
                        applicationService.Codecs = codecs;
                        codecWorker.Succeed();
                        SetState(codecStep, $"Codecs loaded: {applicationService.Codecs.Sum(x => x.Value.Count)}");
                    }
                }

                var formatStep = new InitialisationTask("Get available formats");
                applicationService.InitialisationSteps.Add(formatStep);
                using (var formatWorker = new JobWorker(formatStep.Condition, OnUpdate))
                {
                    logger.LogDebug($"Get available formats.");

                    var result = await fFmpegProcessor.GetAvailableFormatsAsync(applicationService.AppStoppingCancellationToken);

                    if (result.Success)
                    {
                        applicationService.Formats = new(result.Results);
                        formatWorker.Succeed();
                        SetState(formatStep, $"Containers loaded: {applicationService.Formats.Count}");
                    }
                }

                var decoderStep = new InitialisationTask("Get available hardware decoders");
                applicationService.InitialisationSteps.Add(decoderStep);
                using (var decoderWorker = new JobWorker(decoderStep.Condition, OnUpdate))
                {
                    logger.LogDebug($"Get available hardware decoders.");

                    var result = await fFmpegProcessor.GetAvailableHardwareDecodersAsync(applicationService.AppStoppingCancellationToken);

                    if (result.Success)
                    {
                        applicationService.HardwareDecoders = new(result.Results);
                        decoderWorker.Succeed();
                        SetState(decoderStep, $"Hardware decoders loaded: {applicationService.HardwareDecoders.Count}");
                    }
                }

                var encoderStep = new InitialisationTask("Get available encoders");
                applicationService.InitialisationSteps.Add(encoderStep);
                using (var encoderWorker = new JobWorker(encoderStep.Condition, OnUpdate))
                {
                    logger.LogDebug($"Get available encoders.");

                    var encoders = new Dictionary<CodecType, SortedSet<Encoder>>();

                    var result = await fFmpegProcessor.GetAvailableEncodersAsync(applicationService.AppStoppingCancellationToken);

                    if (result.Success)
                    {
                        encoders.Add(CodecType.Audio, new());
                        encoders.Add(CodecType.Subtitle, new());
                        encoders.Add(CodecType.Video, new());

                        foreach (var e in result.Results)
                        {
                            encoders[e.Type].Add(new(e.Name, e.Description, await GetOptionsAsync(e.Name)));
                        }
                        applicationService.Encoders = encoders;
                        encoderWorker.Succeed();
                        SetState(encoderStep, $"Encoders loaded: {applicationService.Encoders.Sum(x => x.Value.Count)}");
                    }

                }

                var demuxerStep = new InitialisationTask("Get available demuxer extensions");
                applicationService.InitialisationSteps.Add(demuxerStep);
                using (var demuxerWorker = new JobWorker(demuxerStep.Condition, OnUpdate))
                {
                    logger.LogDebug($"Get FFmpeg demuxer extensions.");

                    var result = await fFmpegProcessor.GetFFmpegExtensionsAsync(applicationService.Formats, applicationService.AppStoppingCancellationToken);
                    if (result.Success)
                    {
                        applicationService.DemuxerExtensions = new(result.Results);

                        demuxerWorker.Succeed();
                        SetState(demuxerStep, $"Demuxer extensions loaded: {applicationService.DemuxerExtensions.Count}");
                    }
                }
            }
        }

        private async Task InitialisePresets()
        {
            using (logger.BeginScope("Initialising Presets"))
            {
                logger.LogInformation("Initialising Presets");
                if (applicationService.Presets != null)
                {
                    var presetStep = new InitialisationTask("Initialise presets");
                    applicationService.InitialisationSteps.Add(presetStep);
                    using var presetsWorker = new JobWorker(presetStep.Condition, OnUpdate);
                    logger.LogDebug($"Initialise presets");

                    await applicationService.Presets.Where(p => !p.Initialised).AsyncParallelForEach(preset =>
                    {
                        return Task.Run(() =>
                        {
                            var encoder = applicationService.Encoders[CodecType.Video].FirstOrDefault(c => c.Name == preset.VideoEncoder.Name);
                            if (encoder != null)
                            {
                                preset.VideoEncoder = encoder;
                            }

                            foreach (var audioPreset in preset.AudioStreamPresets)
                            {
                                if (audioPreset?.Encoder != null)
                                {
                                    var audioEncoder = applicationService.Encoders[CodecType.Audio].FirstOrDefault(c => c.Name == audioPreset.Encoder.Name);
                                    if (audioEncoder != null)
                                    {
                                        audioPreset.Encoder = audioEncoder;
                                    }
                                }
                            }

                            preset.Initialised = true;
                            SetState(presetStep, $"Presets initialised: {applicationService.Presets.Count(p => p.Initialised)}/{applicationService.Presets.Count}");
                            OnUpdate?.Invoke(this, null);
                        });
                    });


                    presetsWorker.Succeed();
                }
            }
        }

        private void SetState(InitialisationTask task, string state)
        {
            task.State = state;
            logger.LogInformation(state);
        }
    }
}
