using Compressarr.FFmpegFactory.Models;
using Compressarr.Settings;
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

namespace Compressarr.FFmpegFactory
{
    public class FFmpegInitialiser : IFFmpegInitialiser
    {

        private readonly ILogger<FFmpegInitialiser> logger;
        private readonly ISettingsManager settingsManager;
        public FFmpegInitialiser(ILogger<FFmpegInitialiser> logger, ISettingsManager settingsManager)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;
        }

        public event EventHandler OnReady;
        public event EventHandler<string> OnBroadcast;

        public bool Ready { get; private set; }
        public string Version { get; private set; }
        public async Task Start()
        {
            using (logger.BeginScope("Initialising FFMPEG"))
            {
                string existingVersion = null;

                var ffmpegAlreadyExists = SettingsManager.HasFile(FFmpegManager.FFMPEG);
                if (!ffmpegAlreadyExists)
                {
                    logger.LogInformation("Downloading FFmpeg");
                    OnBroadcast?.Invoke(this, "Downloading FFmpeg");
                }
                else
                {
                    existingVersion = await GetFFmpegVersionAsync();
                    logger.LogInformation("Checking for FFmpeg update");
                    OnBroadcast?.Invoke(this, "Checking for FFmpeg update");
                }

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, SettingsManager.GetAppDirPath(AppDir.FFmpeg));
                logger.LogDebug("FFmpeg latest version check finished.");

                if (!SettingsManager.HasFile(FFmpegManager.FFMPEG))
                {
                    throw new FileNotFoundException("FFmpeg not found, download must have failed.");
                }

                Version = await GetFFmpegVersionAsync();

                if (!ffmpegAlreadyExists)
                {
                    logger.LogInformation("FFmpeg downloaded.");
                    OnBroadcast?.Invoke(this, "FFmpeg finished Downloading ");
                }
                else
                {
                    if(existingVersion != Version && Version != null)
                    {
                        logger.LogInformation("FFmpeg updated.");
                        OnBroadcast?.Invoke(this, $"FFmpeg updated to: {Version}");
                    }
                }

                var codecLoader = GetAvailableCodecsAsync();
                var containerLoader = GetAvailableContainersAsync();
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogDebug("Running on Linux, CHMOD required");
                    foreach (var exe in new string[] { FFmpegManager.FFMPEG, FFmpegManager.FFPROBE })
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
                

                settingsManager.Codecs = await codecLoader;
                settingsManager.Containers = await containerLoader;

                Ready = true;
                OnReady?.Invoke(this, null);
                OnBroadcast?.Invoke(this, "FFmpeg Initialisation complete");
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
                p.StartInfo.FileName = FFmpegManager.FFMPEG;
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

                codecs.Add(CodecType.Audio, new());
                codecs.Add(CodecType.Subtitle, new());
                codecs.Add(CodecType.Video, new());

                var regPattern = @"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$";
                var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                foreach (Match m in reg.Matches(output))
                {
                    var codecName = m.Groups[2].Value;
                    var codecDesc = m.Groups[3].Value;
                    var codecOptions = await GetOptionsAsync(codecName);

                    switch (m.Groups[1].Value)
                    {
                        case "A":
                            codecs[CodecType.Audio].Add(new(codecName, codecDesc, codecOptions));
                            break;

                        case "S":
                            codecs[CodecType.Subtitle].Add(new(codecName, codecDesc, codecOptions));
                            break;

                        case "V":
                            codecs[CodecType.Video].Add(new(codecName, codecDesc, codecOptions));
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
            p.StartInfo.FileName = FFmpegManager.FFMPEG;
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
                    if (SettingsManager.HasFile(AppFile.ffmpegVersion))
                    {
                        var version = await settingsManager.ReadJsonFileAsync<dynamic>(AppFile.ffmpegVersion);

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

        private async Task<HashSet<CodecOption>> GetOptionsAsync(string codec)
        {
            using (logger.BeginScope("Get Codec Options"))
            {
                var optionsFile = SettingsManager.GetFilePath(AppDir.CodecOptions, $"{codec}.json");

                if (SettingsManager.HasFile(optionsFile))
                {
                    return await settingsManager.ReadJsonFileAsync<HashSet<CodecOption>>(optionsFile);
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
