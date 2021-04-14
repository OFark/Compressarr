using Compressarr.FFmpegFactory.Interfaces;
using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegManager : IFFmpegManager
    {
        private readonly IServiceProvider services;
        private readonly IWebHostEnvironment env;

        private string ExecutablesPath => Path.Combine(env.ContentRootPath, "config", "FFmpeg");
        private string PresetsFilePath => Path.Combine(env.ContentRootPath, "config", "presets.json");

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

        public FFmpegManager(IServiceProvider services, IWebHostEnvironment env)
        {
            this.services = services;
            this.env = env;

            Status = FFmpegStatus.Initialising;
            FFmpeg.SetExecutablesPath(ExecutablesPath);
        }

        public void Init()
        {
            initTask = Task.Run(async () =>
            {
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ExecutablesPath);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    foreach (var exe in new string[] { FFMPEG, FFPROBE })
                    {
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo()
                            {
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                FileName = "/bin/bash",
                                Arguments = $"-c \"chmod +x {exe}\""
                            };
                            process.StartInfo.RedirectStandardOutput = true;
                            process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                            process.Start();
                            process.WaitForExit();
                        };
                    }
                }
            });

            initTask.Wait();

            Codecs = GetAvailableCodecs();
            Containers = GetAvailableContainers();
            Status = FFmpegStatus.Ready;
        }

        public IFFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public void AddPreset(IFFmpegPreset newPreset)
        {
            initTask.Wait();

            var preset = Presets.FirstOrDefault(x => x.Name == newPreset.Name);

            if (preset == null)
            {
                _presets.Add(newPreset);
            }
            else
            {
                Presets.Remove(preset);
                Presets.Add(newPreset);
            }

            SavePresets();
        }

        public void DeletePreset(string presetName)
        {
            initTask.Wait();

            var preset = Presets.FirstOrDefault(x => x.Name == presetName);

            if (preset != null)
            {
                _presets.Remove(preset);
            }

            SavePresets();
        }

        public bool CheckResult(WorkItem workitem)
        {
            initTask.Wait();

            var mediaInfoTask = FFmpeg.GetMediaInfo(workitem.DestinationFile);
            mediaInfoTask.Wait();
            var mediaInfo = mediaInfoTask.Result;

            return mediaInfo.Duration == workitem.Duration;
        }

        public string ConvertContainerToExtension(string container)
        {
            initTask.Wait();

            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = FFMPEG;
            p.StartInfo.Arguments = $"-v 1 -h muxer={container}";

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var formatLines = output.Split("\r\n").ToList();

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
            }

            return container;
        }

        private HashSet<IFFmpegPreset> LoadPresets()
        {
            initTask.Wait();

            _presets = new HashSet<IFFmpegPreset>();

            if (File.Exists(PresetsFilePath))
            {
                var json = File.ReadAllText(PresetsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var presets = JsonConvert.DeserializeObject<HashSet<FFmpegPreset>>(json);

                    foreach(var preset in presets)
                    {
                        if(preset.VideoCodecOptions != null && preset.VideoCodecOptions.Any())
                        {
                            var codecOptions = GetOptions(preset.VideoCodec);
                            foreach(var co in codecOptions)
                            {
                                var val = preset.VideoCodecOptions.FirstOrDefault(x => x.Name == co.Name);
                                if(val != null)
                                {
                                    co.Value = val.Value;
                                }
                            }

                            preset.VideoCodecOptions = codecOptions;
                        }
                        _presets.Add(preset);
                    }
                }
            }

            return _presets;
        }

        private void SavePresets()
        {
            initTask.Wait();

            var json = JsonConvert.SerializeObject(Presets, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(PresetsFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsFilePath));
            }

            File.WriteAllText(PresetsFilePath, json);
        }

        private Dictionary<CodecType, SortedDictionary<string, string>> GetAvailableCodecs()
        {
            initTask.Wait();

            var codecs = new Dictionary<CodecType, SortedDictionary<string, string>>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = FFMPEG;
            p.StartInfo.Arguments = "-encoders";

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var codecLines = output.Split("\r\n").ToList().Skip(10);

            codecs.Add(CodecType.Audio, new SortedDictionary<string, string>());
            codecs.Add(CodecType.Subtitle, new SortedDictionary<string, string>());
            codecs.Add(CodecType.Video, new SortedDictionary<string, string>());

            foreach (var line in codecLines)
            {
                var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (!string.IsNullOrEmpty(line) && parts.Length >= 3)
                {
                    var codecName = parts[1];
                    var codecDesc = string.Join(" ", parts.Skip(2));
                    switch (line.Trim().Substring(0, 1))
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
                    }
                }
            }

            return codecs;
        }

        private SortedDictionary<string, string> GetAvailableContainers()
        {
            initTask.Wait();

            var formats = new SortedDictionary<string, string>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = FFMPEG;
            p.StartInfo.Arguments = "-formats -v 1";

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var formatLines = output.Split("\r\n").ToList().Skip(4);

            foreach (var line in formatLines)
            {
                var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (!string.IsNullOrEmpty(line) && parts.Length >= 3)
                {
                    var formatName = parts[1];
                    var formatDesc = string.Join(" ", parts.Skip(2));
                    if (line.Substring(2, 1) == "E")
                    {
                        formats.Add(formatName, formatDesc);
                    }
                }
            }

            return formats;
        }

        public HashSet<CodecOptionValue> GetOptions(string codec)
        {
            var optionsFile = Path.Combine(env.ContentRootPath, "codecoptions", $"{codec}.json");

            if (File.Exists(optionsFile))
            {
                var json = File.ReadAllText(optionsFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var cov = new HashSet<CodecOptionValue>();
                    var options = JsonConvert.DeserializeObject<HashSet<CodecOptionValue>>(json);
                    return options;
                }
            }
            return null;
        }
    }
}