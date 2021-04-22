using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegManager : IFFmpegManager
    {
        private const string presetsFile = "presets.json";
        private readonly ILogger<FFmpegManager> logger;
        private readonly IServiceProvider services;
        private readonly ISettingsManager settingsManager;
        private Task initTask = null;
        public FFmpegManager(IServiceProvider services, ISettingsManager settingsManager, ILogger<FFmpegManager> logger)
        {
            this.services = services;
            this.settingsManager = settingsManager;
            this.logger = logger;

            Status = FFmpegStatus.Initialising;
            FFmpeg.SetExecutablesPath(ExecutablesPath);
        }

        public SortedDictionary<string, string> AudioCodecs => Codecs[CodecType.Audio];
        public SortedDictionary<string, string> Containers { get; private set; }
        public HashSet<IFFmpegPreset> Presets => _presets ?? LoadPresets();
        public FFmpegStatus Status { get; private set; }
        public SortedDictionary<string, string> SubtitleCodecs => Codecs[CodecType.Subtitle];
        public SortedDictionary<string, string> VideoCodecs => Codecs[CodecType.Video];
        private HashSet<IFFmpegPreset> _presets { get; set; }
        private Dictionary<CodecType, SortedDictionary<string, string>> Codecs { get; set; }
        private string ExecutablesPath => Path.Combine(SettingsManager.ConfigDirectory, "FFmpeg");
        private string FFMPEG => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(ExecutablesPath, "ffmpeg.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Path.Combine(ExecutablesPath, "ffmpeg")
                               : throw new NotSupportedException("Cannot Identify OS");

        private string FFPROBE => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(ExecutablesPath, "ffprobe.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Path.Combine(ExecutablesPath, "ffprobe")
                               : throw new NotSupportedException("Cannot Identify OS");
        public void AddPreset(IFFmpegPreset newPreset)
        {
            initTask.Wait();
            using (logger.BeginScope("Adding Preset"))
            {
                logger.LogInformation($"Preset Name: {newPreset.Name}");

                var preset = Presets.FirstOrDefault(x => x.Name == newPreset.Name);

                if (preset == null)
                {
                    logger.LogDebug("Adding a new preset.");
                    _presets.Add(newPreset);
                }
                else
                {
                    logger.LogDebug("Preset already exists, removing the old one.");
                    Presets.Remove(preset);
                    logger.LogDebug("Adding the new one.");
                    Presets.Add(newPreset);
                }

                SavePresets();
            }
        }

        public async Task<WorkItemCheckResult> CheckResult(Job job)
        {
            initTask.Wait();

            using (logger.BeginScope("Checking Results."))
            {
                if (job?.Process?.WorkItem == null)
                {
                    return null;
                }
                var result = new WorkItemCheckResult(job.Process.WorkItem);

                var mediaInfo = await GetMediaInfo(job.Process.WorkItem.DestinationFile);
                if (mediaInfo != null)
                {
                    //Workitem.Duration refers to the processing time frame.
                    logger.LogDebug($"Original Duration: {job.Process.WorkItem.TotalLength}");
                    logger.LogDebug($"New Duration: {mediaInfo.Duration}");

                    result.LengthOK = mediaInfo.Duration != default && job.Process.WorkItem.TotalLength.HasValue &&
                        (long)Math.Round(mediaInfo.Duration.TotalSeconds, 0) == (long)Math.Round(job.Process.WorkItem.TotalLength.Value.TotalSeconds, 0);
                }

                result.SSIMOK = result.LengthOK &&
                    (!job.SSIMCheck || job.MinSSIM <= job.Process.WorkItem.SSIM);


                result.SizeOK = result.SSIMOK &&
                    (!job.SizeCheck || job.MaxCompression >= job.Process.WorkItem.Compression);

                return result;
            }
        }

        public string ConvertContainerToExtension(string container)
        {
            initTask.Wait();
            using (logger.BeginScope("Converting container to extension"))
            {
                logger.LogInformation($"Container name: {container}");

                var p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = FFMPEG;
                p.StartInfo.Arguments = $"-v 1 -h muxer={container}";

                logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    logger.LogError($"Process Error: ({p.ExitCode}) {error} <End Of Error>");
                }

                var formatLines = output.Split("\n").ToList();

                foreach (var line in formatLines)
                {
                    if (line.Trim().StartsWith("Common extensions:"))
                    {
                        var lineSplit = line.Split(":");
                        if (lineSplit.Length == 2)
                        {
                            var extensions = lineSplit[1].Trim().TrimEnd('.');

                            var splitExtensions = extensions.Split(",");

                            if (splitExtensions.Length > 0)
                            {
                                return splitExtensions[0];
                            }
                        }
                    }
                    else
                    {
                        logger.LogDebug($"No common extensions found.");
                    }
                }

                return container;
            }
        }

        public void DeletePreset(string presetName)
        {
            initTask.Wait();

            using (logger.BeginScope("Deleting Preset"))
            {
                logger.LogInformation($"Preset Name: {presetName}");

                var preset = Presets.FirstOrDefault(x => x.Name == presetName);

                if (preset != null)
                {
                    _presets.Remove(preset);
                }
                else
                {
                    logger.LogWarning($"Preset {presetName} not found.");
                }

                SavePresets();
            }
        }

        public string GetFFmpegVersion()
        {
            using (logger.BeginScope("Get FFmpeg Version."))
            {
                var path = Path.Combine(ExecutablesPath, "version.json");
                logger.LogDebug($"From {path}.");

                try
                {
                    var json = File.ReadAllText(path);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var version = JsonConvert.DeserializeObject<dynamic>(json);
                        return version.version.ToString();
                    }
                    else
                    {
                        logger.LogDebug($"Empty file.");
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

        public async Task<IMediaInfo> GetMediaInfo(string filepath)
        {
            using (logger.BeginScope($"Getting MediaInfo"))
            {
                logger.LogInformation("File Name: {filepath}");
                try
                {
                    return await FFmpeg.GetMediaInfo(filepath);
                }
                catch (ArgumentException aex)
                {
                    logger.LogError(aex.ToString());
                    return null;
                }
            }
        }

        public HashSet<CodecOptionValue> GetOptions(string codec)
        {
            using (logger.BeginScope("Get Codec Options"))
            {
                logger.LogInformation($"Codec: {codec}");

                var optionsFile = Path.Combine(SettingsManager.CodecOptionsDirectory, $"{codec}.json");
                logger.LogDebug($"From {optionsFile}");

                if (File.Exists(optionsFile))
                {
                    var json = File.ReadAllText(optionsFile);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        HashSet<CodecOptionValue> options = null;
                        try
                        {
                            options = JsonConvert.DeserializeObject<HashSet<CodecOptionValue>>(json);
                        }
                        catch (JsonSerializationException jsex)
                        {
                            logger.LogError($"JSON parsing error: {jsex}.");
                        }
                        return options;
                    }
                    else
                    {
                        logger.LogDebug($"Options file empty.");
                    }
                }
                else
                {
                    logger.LogDebug($"Codec Options file not found.");
                }
                return null;
            }
        }

        public IFFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public void Init()
        {
            using (logger.BeginScope("Initialising FFMPEG"))
            {
                initTask = Task.Run(async () =>
                {
                    logger.LogInformation("Getting latest version");
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ExecutablesPath);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        logger.LogDebug("Running on Linux, CHMOD required");
                        foreach (var exe in new string[] { FFMPEG, FFPROBE })
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
                });

                initTask.Wait();

                logger.LogDebug("FFmpeg downloaded.");

                Codecs = GetAvailableCodecs();
                Containers = GetAvailableContainers();
                Status = FFmpegStatus.Ready;
            }
        }
        private Dictionary<CodecType, SortedDictionary<string, string>> GetAvailableCodecs()
        {
            initTask.Wait();

            logger.LogDebug($"Get available codecs.");

            var codecs = new Dictionary<CodecType, SortedDictionary<string, string>>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = FFMPEG;
            p.StartInfo.Arguments = "-encoders -v 1";

            logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");

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
        }

        private SortedDictionary<string, string> GetAvailableContainers()
        {
            initTask.Wait();

            logger.LogDebug($"Get Available Containers.");

            var formats = new SortedDictionary<string, string>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = FFMPEG;
            p.StartInfo.Arguments = "-formats -v 1";

            logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");

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
        }

        private HashSet<IFFmpegPreset> LoadPresets()
        {
            using (logger.BeginScope("Load Presets"))
            {
                initTask.Wait();

                logger.LogInformation($"Presets empty");

                _presets = new HashSet<IFFmpegPreset>();

                var presets = settingsManager.LoadSettingFile<HashSet<FFmpegPreset>>(presetsFile).Result ?? new();

                foreach (var preset in presets)
                {
                    if (preset.VideoCodecOptions != null && preset.VideoCodecOptions.Any())
                    {
                        var codecOptions = GetOptions(preset.VideoCodec);
                        foreach (var co in codecOptions)
                        {
                            var val = preset.VideoCodecOptions.FirstOrDefault(x => x.Name == co.Name);
                            if (val != null)
                            {
                                co.Value = val.Value;
                            }
                        }

                        preset.VideoCodecOptions = codecOptions;
                    }
                    _presets.Add(preset);
                }

                return _presets;
            }
        }

        private void SavePresets()
        {
            using (logger.BeginScope("Save Presets"))
            {
                initTask.Wait();
                logger.LogInformation($"Saving Presets");

                settingsManager.SaveSettingFile(presetsFile, Presets);
            }
        }
    }
}