using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
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
        private readonly IServiceProvider services;
        private readonly ISettingsManager settingsManager;
        private readonly ILogger<FFmpegManager> logger;

        private string ExecutablesPath => Path.Combine(SettingsManager.ConfigDirectory, "FFmpeg");
        private string PresetsFilePath => settingsManager?.ConfigFile("presets.json");

        private string FFMPEG => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(ExecutablesPath, "ffmpeg.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Path.Combine(ExecutablesPath, "ffmpeg")
                               : throw new NotSupportedException("Cannot Identify OS");

        private string FFPROBE => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(ExecutablesPath, "ffprobe.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Path.Combine(ExecutablesPath, "ffprobe")
                               : throw new NotSupportedException("Cannot Identify OS");


        private HashSet<IFFmpegPreset> _presets { get; set; }
        public HashSet<IFFmpegPreset> Presets => _presets ?? LoadPresets();

        public FFmpegStatus Status { get; private set; }

        public SortedDictionary<string, string> AudioCodecs => Codecs[CodecType.Audio];
        public SortedDictionary<string, string> Containers { get; private set; }
        public SortedDictionary<string, string> SubtitleCodecs => Codecs[CodecType.Subtitle];
        public SortedDictionary<string, string> VideoCodecs => Codecs[CodecType.Video];

        private Dictionary<CodecType, SortedDictionary<string, string>> Codecs { get; set; }

        private Task initTask = null;

        public FFmpegManager(IServiceProvider services, ISettingsManager settingsManager, ILogger<FFmpegManager> logger)
        {
            this.services = services;
            this.settingsManager = settingsManager;
            this.logger = logger;

            Status = FFmpegStatus.Initialising;
            FFmpeg.SetExecutablesPath(ExecutablesPath);
        }

        public void Init()
        {
            logger.LogInformation("Initialising FFMPEG");
            initTask = Task.Run(async () =>
            {
                logger.LogDebug("Getting latest version");
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

        public IFFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public void AddPreset(IFFmpegPreset newPreset)
        {
            initTask.Wait();
            logger.LogDebug($"Adding Preset: {newPreset.Name}.");

            var preset = Presets.FirstOrDefault(x => x.Name == newPreset.Name);

            if (preset == null)
            {
                logger.LogDebug("New Preset.");
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

        public void DeletePreset(string presetName)
        {
            initTask.Wait();

            logger.LogDebug($"Deleting Preset: {presetName}.");

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

        public async Task<bool> CheckResult(WorkItem workitem)
        {
            initTask.Wait();

            logger.LogDebug("Checking Results.");


            var mediaInfo = await GetMediaInfo(workitem.DestinationFile);
            if (mediaInfo != null)
            {
                //Workitem.Duration refers to the processing time frame.
                logger.LogDebug($"Original Duration: {workitem.TotalLength}");
                logger.LogDebug($"New Duration: {mediaInfo.Duration}");

                return mediaInfo.Duration != default && workitem.TotalLength.HasValue &&
                    (long)Math.Round(mediaInfo.Duration.TotalSeconds, 0) == (long)Math.Round(workitem.TotalLength.Value.TotalSeconds, 0);
            }
            return false;
        }

        public async Task<IMediaInfo> GetMediaInfo(string filepath)
        {
            logger.LogDebug($"Getting MediaInfo ({filepath}).");
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

        public string ConvertContainerToExtension(string container)
        {
            initTask.Wait();

            logger.LogDebug($"Converting container name ({container}) to file extension.");

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

        public string GetFFmpegVersion()
        {
            logger.LogDebug($"Get FFmpeg Version.");

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

        private HashSet<IFFmpegPreset> LoadPresets()
        {
            initTask.Wait();
            logger.LogDebug($"Presets empty - loading from file ({PresetsFilePath}).");

            _presets = new HashSet<IFFmpegPreset>();

            if (File.Exists(PresetsFilePath))
            {
                var json = File.ReadAllText(PresetsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var presets = JsonConvert.DeserializeObject<HashSet<FFmpegPreset>>(json);

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
                }
                else
                {
                    logger.LogDebug($"Presets file is empty.");
                }
            }
            else
            {
                logger.LogDebug($"Presets file does not exist.");
            }

            return _presets;
        }

        private void SavePresets()
        {
            initTask.Wait();
            logger.LogDebug($"Saving Presets to {PresetsFilePath}.");

            var json = JsonConvert.SerializeObject(Presets, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(PresetsFilePath)))
            {
                logger.LogDebug($"Directory does not exist. Creating.");
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsFilePath));
            }

            File.WriteAllText(PresetsFilePath, json);
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

        public HashSet<CodecOptionValue> GetOptions(string codec)
        {
            logger.LogDebug($"Get options for Codec {codec}.");
            var optionsFile = Path.Combine(SettingsManager.CodecOptionsDirectory, $"{codec}.json");
            logger.LogDebug($"From {optionsFile}.");

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
}