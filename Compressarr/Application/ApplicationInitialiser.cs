using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.FFmpeg.Models;
using Compressarr.JobProcessing;
using Compressarr.Presets.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.Presets
{
    public class ApplicationInitialiser : IApplicationInitialiser
    {

        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "This may come in handy")]
        private readonly Task InitialisationTask;

        private readonly IJobManager jobManager;
        private readonly ILogger<ApplicationInitialiser> logger;
        public ApplicationInitialiser(IApplicationService applicationService, IFileService fileService, IJobManager jobManager, ILogger<ApplicationInitialiser> logger, IFFmpegProcessor fFmpegProcessor)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.jobManager = jobManager;
            this.logger = logger;

            InitialisationTask = Task.Run(() => InitialiseAsync());
        }

        public async Task InitialiseAsync()
        {
            using (logger.BeginScope("Initialising Application"))
            {
                try
                {
                    applicationService.InitialiseFFmpeg = InitialiseFFmpeg();

                    applicationService.InitialisePresets = InitialisePresets();

                    Progress("Application Initialisation complete");

                    if (applicationService.Jobs != null)
                    {
                        Progress("Initialising Jobs");

                        foreach (var job in applicationService.Jobs.Where(j => !j.Initialised))
                        {
                            job.StatusUpdate += Job_StatusUpdate;
                            await jobManager.InitialiseJob(job, applicationService.AppStoppingCancellationToken);
                            job.StatusUpdate -= Job_StatusUpdate;
                        }
                        Progress("Job Initialisation complete");
                    }


                    Progress("Ready");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Initialisation Error");
                }
            }
        }

        private async Task<Dictionary<CodecType, SortedSet<Codec>>> GetAvailableCodecsAsync()
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
                return codecs;
            }
            return null;
        }

        private async Task<SortedSet<FFmpegFormat>> GetAvailableContainersAsync()
        {
            logger.LogDebug($"Get Available Containers.");

            var result = await fFmpegProcessor.GetAvailableFormatsAsync(applicationService.AppStoppingCancellationToken);

            if (result.Success)
            {
                return new(result.Results);
            }

            return null;
        }

        private async Task<Dictionary<CodecType, SortedSet<Encoder>>> GetAvailableEncodersAsync()
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

                return encoders;
            }

            return null;
        }

        private async Task<SortedSet<string>> GetAvailableHardwareDecodersAsync()
        {
            logger.LogDebug($"Get available hardware decoders.");

            var result = await fFmpegProcessor.GetAvailableHardwareDecodersAsync(applicationService.AppStoppingCancellationToken);

            if (result.Success)
            {
                return new(result.Results);
            }

            return null;

        }

        private async Task<SortedSet<string>> GetFFmpegDemuxerExtensions()
        {
            logger.LogDebug($"Get FFmpeg demuxer extensions.");

            var result = await fFmpegProcessor.GetFFmpegExtensionsAsync(applicationService.Formats, applicationService.AppStoppingCancellationToken);

            if (result.Success)
            {
                return new(result.Results);
            }

            return null;
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
            string existingVersion = null;

            Progress("Initialising FFmpeg");

            if (!AppEnvironment.InNvidiaDocker)
            {
                var ffmpegAlreadyExists = fileService.HasFile(fileService.FFMPEGPath);
                if (!ffmpegAlreadyExists)
                {
                    Progress("Downloading FFmpeg");
                }
                else
                {
                    existingVersion = await GetFFmpegVersionAsync();
                    Progress("Checking for FFmpeg update");
                }

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, fileService.GetAppDirPath(AppDir.FFmpeg), new Progress<ProgressInfo>(ReportFFmpegProgress));
                logger.LogDebug("FFmpeg latest version check finished.");

                if (!fileService.HasFile(fileService.FFMPEGPath))
                {
                    throw new FileNotFoundException("FFmpeg not found, download must have failed.");
                }


                if (!ffmpegAlreadyExists)
                {
                    Progress("FFmpeg finished Downloading ");
                }
                else
                {
                    if (existingVersion != applicationService.FFmpegVersion && applicationService.FFmpegVersion != null)
                    {
                        Progress($"FFmpeg updated to: {applicationService.FFmpegVersion}");
                    }
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogDebug("Running on Linux, CHMOD required");
                    foreach (var exe in new string[] { fileService.FFMPEGPath, fileService.FFPROBEPath })
                    {
                        await fFmpegProcessor.RunProcess("/bin/bash", $"-c \"chmod +x {exe}\"", applicationService.AppStoppingCancellationToken);
                    }
                }
            }
            else
            {
                Progress("Nvidia Docker, skipping FFMpeg download");
            }

            var versionLoader = GetFFmpegVersionAsync();
            var codecLoader = GetAvailableCodecsAsync();
            var formatLoader = GetAvailableContainersAsync();
            var decoderLoader = GetAvailableHardwareDecodersAsync();
            var encoderLoader = GetAvailableEncodersAsync();

            applicationService.FFmpegVersion = await versionLoader;
            applicationService.Codecs = await codecLoader;
            applicationService.Formats = await formatLoader;
            applicationService.Encoders = await encoderLoader;
            applicationService.HardwareDecoders = await decoderLoader;

            applicationService.DemuxerExtensions = await GetFFmpegDemuxerExtensions();

            Progress("FFmpeg Initialisation complete");
        }

        private async Task InitialisePresets()
        {
            await applicationService.InitialiseFFmpeg;
            using (logger.BeginScope("Initialising Presets"))
            {
                Progress("Initialising Presets");
                if (applicationService.Presets != null)
                {
                    foreach (var preset in applicationService.Presets.Where(p => !p.Initialised))
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
                    }
                }
            }
        }

        private void Job_StatusUpdate(object sender, EventArgs e)
        {
            applicationService.Progress = (double)100 * jobManager.Jobs.Sum(j => j.WorkLoad?.Count(x => x.Arguments != null) ?? 0) / jobManager.Jobs.Sum(j => j.WorkLoad?.Count ?? 1);
            applicationService.Broadcast("");
        }

        private void Progress(string message)
        {
            applicationService.StateHistory.Enqueue(message);
            applicationService.State = message;
            logger.LogInformation(message);
            applicationService.Broadcast(message);
        }
        private void ReportFFmpegProgress(ProgressInfo info)
        {
            applicationService.Progress = 100 * info.DownloadedBytes / info.TotalBytes;
            applicationService.Broadcast("");
        }
    }
}
