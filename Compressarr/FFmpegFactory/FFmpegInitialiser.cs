using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegInitialiser : IFFmpegInitialiser
    {

        private readonly ISettingsManager settingsManager;
        private readonly ILogger<FFmpegInitialiser> logger;

        public string Version { get; private set; }

        public bool Ready { get; private set; }

        public FFmpegInitialiser(ILogger<FFmpegInitialiser> logger, ISettingsManager settingsManager)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;
        }

        public async Task Start()
        {
            using (logger.BeginScope("Initialising FFMPEG"))
            {
                logger.LogInformation("Getting latest version");
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, SettingsManager.GetAppDirPath(AppDir.FFmpeg));

                var codecLoader = GetAvailableCodecsAsync();
                var containerLoader = GetAvailableContainersAsync();
                var versionLoader = GetFFmpegVersionAsync();

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
                            p.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
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
                logger.LogDebug("FFmpeg downloaded.");

                Version = await versionLoader;
                settingsManager.Codecs = await codecLoader;
                settingsManager.Containers = await containerLoader;

                Ready = true;
            }
        }

        private Task<Dictionary<CodecType, SortedDictionary<string, string>>> GetAvailableCodecsAsync()
        {
            logger.LogDebug($"Get available codecs.");

            var codecs = new Dictionary<CodecType, SortedDictionary<string, string>>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = FFmpegManager.FFMPEG;
            p.StartInfo.Arguments = "-encoders -v 1";

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

                    switch (m.Groups[1].Value)
                    {
                        case "A":
                            codecs[CodecType.Audio].Add(codecName, codecDesc);
                            break;

                        case "S":
                            codecs[CodecType.Subtitle].Add(codecName, codecDesc);
                            break;

                        case "V":
                            codecs[CodecType.Video].Add(codecName, codecDesc);
                            break;

                        default:
                            logger.LogWarning($"Unrecognised Codec line: {m.Groups[0]}");
                            break;
                    }
                }

                return codecs;
            });
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
    }
}
