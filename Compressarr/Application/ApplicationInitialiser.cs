using Compressarr.FFmpegFactory.Models;
using Compressarr.Application;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg.Downloader;
using System.Linq;
using Compressarr.JobProcessing;
using Compressarr.Application.Interfaces;

namespace Compressarr.FFmpegFactory
{
    public class ApplicationInitialiser : IStartupTask
    {

        private readonly IFileService fileService;
        private readonly IJobManager jobManager;
        private readonly ILogger<ApplicationInitialiser> logger;
        private readonly IApplicationService applicationService;
        public ApplicationInitialiser(IFileService fileService, IJobManager jobManager, ILogger<ApplicationInitialiser> logger, IApplicationService applicationService)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.jobManager = jobManager;
            this.logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using (logger.BeginScope("Initialising Application"))
            {
                string existingVersion = null;

                var ffmpegAlreadyExists = fileService.HasFile(fileService.FFMPEGPath);
                if (!ffmpegAlreadyExists)
                {
                    logger.LogInformation("Downloading FFmpeg");
                    applicationService.Broadcast("Downloading FFmpeg");
                }
                else
                {
                    existingVersion = await GetFFmpegVersionAsync();
                    logger.LogInformation("Checking for FFmpeg update");
                    applicationService.Broadcast("Checking for FFmpeg update");
                }

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, fileService.GetAppDirPath(AppDir.FFmpeg));
                logger.LogDebug("FFmpeg latest version check finished.");

                if (!fileService.HasFile(fileService.FFMPEGPath))
                {
                    throw new FileNotFoundException("FFmpeg not found, download must have failed.");
                }

                applicationService.FFmpegVersion = await GetFFmpegVersionAsync();

                if (!ffmpegAlreadyExists)
                {
                    logger.LogInformation("FFmpeg downloaded.");
                    applicationService.Broadcast("FFmpeg finished Downloading ");
                }
                else
                {
                    if (existingVersion != applicationService.FFmpegVersion && applicationService.FFmpegVersion != null)
                    {
                        logger.LogInformation("FFmpeg updated.");
                        applicationService.Broadcast($"FFmpeg updated to: {applicationService.FFmpegVersion}");
                    }
                }

                var codecLoader = GetAvailableCodecsAsync();
                var encoderLoader = GetAvailableEncodersAsync();
                var containerLoader = GetAvailableContainersAsync();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogDebug("Running on Linux, CHMOD required");
                    foreach (var exe in new string[] { fileService.FFMPEGPath, fileService.FFPROBEPath })
                    {
                        using (var p = new Process())
                        {
                            p.StartInfo = new ProcessStartInfo()
                            {
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                FileName = "/bin/bash",
                                Arguments = $"-c \"chmod +x {exe}\""
                            };
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.RedirectStandardError = true;
                            p.OutputDataReceived += (sender, args) => logger.LogInformation(args.Data);
                            logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                            p.Start();
                            var error = p.StandardError.ReadToEnd();
                            p.WaitForExit();

                            if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                            {
                                logger.LogError($"Process Error: ({p.ExitCode}) {error} <End Of Error>");
                            }
                            logger.LogDebug("Process finished");
                        };
                    }
                }


                applicationService.Codecs = await codecLoader;
                applicationService.Containers = await containerLoader;
                applicationService.Encoders = await encoderLoader;

                Progress("FFmpeg Initialisation complete");
                
                applicationService.FFMpegReady.Set();

                InitialisePresets();

                Progress("Application Initialisation complete");
                
                applicationService.Initialised.Set();

                if (applicationService.Jobs != null)
                {
                    foreach (var job in applicationService.Jobs.Where(j => !j.Initialised))
                    {
                        await jobManager.InitialiseJob(job);
                    }
                    Progress("Job Initialisation complete");
                }


                Progress("Ready");
            }
        }

        private void Progress(string message)
        {
            logger.LogInformation(message);
            applicationService.Broadcast(message);
            applicationService.State = message;
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
            logger.LogDebug($"Get available codecs.");

            var encoders = new Dictionary<CodecType, SortedSet<Encoder>>();
            using (var p = new Process())
            {
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = fileService.FFMPEGPath;
                p.StartInfo.Arguments = "-encoders -v 1";

                logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                await p.WaitForExitAsync();

                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    logger.LogError($"Process Error: ({p.ExitCode}) {error} <End Of Error>");
                    return null;
                }

                encoders.Add(CodecType.Audio, new());
                encoders.Add(CodecType.Subtitle, new());
                encoders.Add(CodecType.Video, new());

                var regPattern = @"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(output))
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

        }

        private async Task<Dictionary<CodecType, SortedSet<Codec>>> GetAvailableCodecsAsync()
        {
            logger.LogDebug($"Get available codecs.");

            var codecs = new Dictionary<CodecType, SortedSet<Codec>>();
            using (var p = new Process())
            {
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = fileService.FFMPEGPath;
                p.StartInfo.Arguments = "-codecs -v 1";

                logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                await p.WaitForExitAsync();

                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    logger.LogError($"Process Error: ({p.ExitCode}) {error} <End Of Error>");
                    return null;
                }

                codecs.Add(CodecType.Audio, new());
                codecs.Add(CodecType.Subtitle, new());
                codecs.Add(CodecType.Video, new());

                var regPattern = @"^\s([D\.])([E\.])([VAS])[I.][L\.][S\.]\s(?!=)([^\s]*)\s*(.*)$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(output))
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

        }

        private Task<SortedDictionary<string, string>> GetAvailableContainersAsync()
        {
            logger.LogDebug($"Get Available Containers.");

            var formats = new SortedDictionary<string, string>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = fileService.FFMPEGPath;
            p.StartInfo.Arguments = "-formats -v 1";

            logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");

            return Task.Run(() =>
            {
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    logger.LogError($"Process Error: ({p.ExitCode}) {error} <End Of Error>");
                }

                var regPattern = @"^\s?([D\s])([E\s])\s(?!=)([^\s]*)\s*(.*)$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(output))
                {
                    if (m.Groups[2].Value == "E")
                    {
                        formats.Add(m.Groups[3].Value, m.Groups[4].Value);
                    }
                }

                return formats;
            });
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
                        logger.LogDebug($"Version file missing.");
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
                    logger.LogDebug($"Codec Options file not found.");
                }
                return null;
            }
        }
    }
}
