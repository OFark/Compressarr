using Compressarr.Application;
using Compressarr.Application.Models;
using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.FFmpegFactory
{
    public class ApplicationInitialiser : IApplicationInitialiser
    {

        private readonly IFileService fileService;
        private readonly IJobManager jobManager;
        private readonly ILogger<ApplicationInitialiser> logger;
        private readonly IApplicationService applicationService;


        private readonly Task InitialisationTask;

        public ApplicationInitialiser(IFileService fileService, IJobManager jobManager, ILogger<ApplicationInitialiser> logger, IApplicationService applicationService)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.jobManager = jobManager;
            this.logger = logger;

            InitialisationTask = Task.Run(() => InitialiseAsync());
        }

        public async Task InitialiseAsync(CancellationToken cancellationToken = default)
        {
            using (logger.BeginScope("Initialising Application"))
            {
                try
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

                        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, fileService.GetAppDirPath(AppDir.FFmpeg), new Progress<ProgressInfo>(reportFFmpegProgress));
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
                                await applicationService.RunProcess("/bin/bash", $"-c \"chmod +x {exe}\"");
                            }
                        }
                    }
                    else
                    {
                        Progress("Nvidia Docker, skipping FFMpeg download");
                    }

                    applicationService.FFmpegVersion = await GetFFmpegVersionAsync();


                    var codecLoader = GetAvailableCodecsAsync();
                    var containerLoader = GetAvailableContainersAsync();
                    var decoderLoader = GetAvailableHardwareDecodersAsync();
                    var encoderLoader = GetAvailableEncodersAsync();

                    applicationService.Codecs = await codecLoader;
                    applicationService.Containers = await containerLoader;
                    applicationService.Encoders = await encoderLoader;
                    applicationService.HardwareDecoders = await decoderLoader;

                    Progress("FFmpeg Initialisation complete");

                    //todo: replace this with Task
                    applicationService.FFMpegReady.Set();

                    Progress("Initialising Presets");

                    InitialisePresets();

                    Progress("Application Initialisation complete");

                    applicationService.Initialised.Set();

                    if (applicationService.Jobs != null)
                    {
                        Progress("Initialising Jobs");

                        foreach (var job in applicationService.Jobs.Where(j => !j.Initialised))
                        {
                            job.StatusUpdate += Job_StatusUpdate;
                            await jobManager.InitialiseJob(job);
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

        private void Job_StatusUpdate(object sender, EventArgs e)
        {
            applicationService.Progress = (double)100 * jobManager.Jobs.Sum(j => j.WorkLoad?.Count(x => x.MediaInfo != null) ?? 0) / jobManager.Jobs.Sum(j => j.WorkLoad?.Count ?? 1);
            applicationService.Broadcast("");
        }

        private void Progress(string message)
        {
            applicationService.StateHistory.Append(message);
            applicationService.State = message;
            logger.LogInformation(message);
            applicationService.Broadcast(message);
        }

        private void InitialisePresets()
        {
            using (logger.BeginScope("Initialising Presets"))
            {
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

        private async Task<Dictionary<CodecType, SortedSet<Encoder>>> GetAvailableEncodersAsync()
        {
            logger.LogDebug($"Get available encoders.");

            var encoders = new Dictionary<CodecType, SortedSet<Encoder>>();

            var result = await applicationService.RunProcess(fileService.FFMPEGPath, "-encoders -v 1");

            if (result.Success)
            {
                encoders.Add(CodecType.Audio, new());
                encoders.Add(CodecType.Subtitle, new());
                encoders.Add(CodecType.Video, new());

                var regPattern = @"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(result.StdOut))
                {
                    var encoderName = m.Groups[2].Value;
                    var encoderDesc = m.Groups[3].Value;
                    var encoderOptions = await GetOptionsAsync(encoderName);

                    switch (m.Groups[1].Value)
                    {
                        case "A":
                            encoders[CodecType.Audio].Add(new(encoderName, encoderDesc, encoderOptions));
                            break;

                        case "S":
                            encoders[CodecType.Subtitle].Add(new(encoderName, encoderDesc, encoderOptions));
                            break;

                        case "V":
                            encoders[CodecType.Video].Add(new(encoderName, encoderDesc, encoderOptions));
                            break;

                        default:
                            logger.LogWarning($"Unrecognised Encoder line: {m.Groups[0]}");
                            break;
                    }
                }

                return encoders;
            }

            return null;
        }

        private async Task<SortedSet<string>> GetAvailableHardwareDecodersAsync()
        {
            logger.LogDebug($"Get available hardware decoders.");

            var hwdecoders = new SortedSet<string>();

            var result = await applicationService.RunProcess(fileService.FFMPEGPath, "-hwaccels -v 1");

            if (result.Success)
            {
                var regPattern = @"^(?!Hardware).*";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(result.StdOut))
                {
                    if (!string.IsNullOrWhiteSpace(m.Value))
                    {
                        hwdecoders.Add(m.Value.Trim());
                    }
                }

                return hwdecoders;
            }

            return null;

        }

        private async Task<Dictionary<CodecType, SortedSet<Codec>>> GetAvailableCodecsAsync()
        {
            logger.LogDebug($"Get available codecs.");

            var codecs = new Dictionary<CodecType, SortedSet<Codec>>();

            var result = await applicationService.RunProcess(fileService.FFMPEGPath, "-codecs -v 1");

            if (result.Success)
            {
                codecs.Add(CodecType.Audio, new());
                codecs.Add(CodecType.Subtitle, new());
                codecs.Add(CodecType.Video, new());

                var regPattern = @"^\s([D\.])([E\.])([VAS])[I.][L\.][S\.]\s(?!=)([^\s]*).*$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(result.StdOut))
                {
                    var decoder = m.Groups[1].Value == "D";
                    var encoder = m.Groups[2].Value == "E";
                    var codecName = m.Groups[4].Value;
                    var codecDesc = m.Groups[5].Value;

                    var codec = new Codec(codecName, codecDesc, decoder, encoder);

                    switch (m.Groups[3].Value)
                    {
                        case "A":
                            codecs[CodecType.Audio].Add(codec);
                            break;

                        case "S":
                            codecs[CodecType.Subtitle].Add(codec);
                            break;

                        case "V":
                            codecs[CodecType.Video].Add(codec);
                            break;

                        default:
                            logger.LogWarning($"Unrecognised Codec line: {m.Groups[0]}");
                            break;
                    }
                }

                return codecs;

            }
            return null;
        }

        private async Task<SortedDictionary<string, string>> GetAvailableContainersAsync()
        {
            logger.LogDebug($"Get Available Containers.");

            var formats = new SortedDictionary<string, string>();

            var result = await applicationService.RunProcess(fileService.FFMPEGPath, "-formats -v 1");

            if (result.Success)
            {
                var regPattern = @"^ ?([D ])([E ]) (?!=)([^ ]*) *([^\r]*)\r$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(result.StdOut))
                {
                    if (m.Groups[2].Value == "E")
                    {
                        formats.Add(m.Groups[3].Value, m.Groups[4].Value);
                    }
                }

                return formats;
            }

            return null;
        }

        private async Task<string> GetFFmpegVersionAsync()
        {
            using (logger.BeginScope("Get FFmpeg Version."))
            {
                try
                {
                    if (fileService.HasFile(AppFile.ffmpegVersion))
                    {
                        var version = await fileService.ReadJsonFileAsync<dynamic>(AppFile.ffmpegVersion);

                        if (version != null)
                        {
                            return version.version?.ToString();
                        }
                        else
                        {
                            logger.LogDebug($"Empty file.");
                        }
                    }
                    else
                    {
                        logger.LogDebug($"Version file missing. Trying manually");

                        var result = await applicationService.RunProcess(fileService.FFMPEGPath, "-version");

                        if(result.Success)
                        {
                            var reg = new Regex(@"(?<=version\s)(.*)(?=\sCopy)");
                            var match = reg.Match(result.StdOut);
                            if (match != null)
                            {
                                return match.Value;
                            }
                        }

                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    logger.LogError(uae.ToString());
                    return uae.Message;
                }
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

        private void reportFFmpegProgress(ProgressInfo info)
        {
            applicationService.Progress = 100 * info.DownloadedBytes / info.TotalBytes;
            applicationService.Broadcast("");
        }
    }
}
