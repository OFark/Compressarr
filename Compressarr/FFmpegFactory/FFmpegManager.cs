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
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegManager
    {
        private readonly IServiceProvider services;
        private readonly IWebHostEnvironment env;

        private string ExecutablesPath => Path.Combine(env.ContentRootPath, "config", "FFmpeg");
        private string PresetsFilePath => Path.Combine(env.ContentRootPath, "config", "presets.json");

        private HashSet<IFFmpegPreset> _presets { get; set; }
        public HashSet<IFFmpegPreset> Presets => _presets ?? LoadPresets();

        public FFmpegStatus Status { get; private set; }

        public SortedDictionary<string, string> AudioCodecs => Codecs[CodecType.Audio];
        public SortedDictionary<string, string> Containers { get; private set; }
        public SortedDictionary<string, string> SubtitleCodecs => Codecs[CodecType.Subtitle];
        public SortedDictionary<string, string> VideoCodecs => Codecs[CodecType.Video];

        private Dictionary<CodecType, SortedDictionary<string, string>> Codecs { get; set; }

        public FFmpegManager(IServiceProvider services, IWebHostEnvironment env)
        {
            this.services = services;
            this.env = env;

            Status = FFmpegStatus.Initialising;
            FFmpeg.SetExecutablesPath(ExecutablesPath);
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ExecutablesPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var exe in new string[] { "ffmpeg", "ffprobe" })
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
                            Arguments = $"-c \"chmod +x {Path.Combine(ExecutablesPath, exe)}\""
                        };
                        process.StartInfo.RedirectStandardOutput = true;
                        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                        process.Start();
                        process.WaitForExit();
                    };
                }
            }

            Codecs = GetAvailableCodecs();
            Containers = GetAvailableContainers();
            Status = FFmpegStatus.Ready;
        }

        internal IFFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public void AddPreset(IFFmpegPreset newPreset)
        {
            var preset = Presets.FirstOrDefault(x => x.Name == newPreset.Name);

            if (preset == null)
            {
                _presets.Add(newPreset);
            }
            else
            {
                preset = newPreset;
            }

            SavePresets();
        }

        public void DeletePreset(string presetName)
        {
            var preset = Presets.FirstOrDefault(x => x.Name == presetName);

            if (preset != null)
            {
                _presets.Remove(preset);
            }

            SavePresets();
        }

        public bool CheckResult(WorkItem workitem)
        {
            var mediaInfoTask = FFmpeg.GetMediaInfo(workitem.DestinationFile);
            mediaInfoTask.Wait();
            var mediaInfo = mediaInfoTask.Result;

            return mediaInfo.Duration == workitem.Duration;
        }

        public string ConvertContainerToExtension(string container)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg");
            }

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
            _presets = new HashSet<IFFmpegPreset>();

            if (File.Exists(PresetsFilePath))
            {
                var json = File.ReadAllText(PresetsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var dynamicpresets = JsonConvert.DeserializeObject<HashSet<dynamic>>(json);

                    foreach (var dp in dynamicpresets)
                    {
                        var j = JsonConvert.SerializeObject(dp);

                        switch (dp.VideoCodec.Value)
                        {
                            case "libx264":
                                {
                                    var preset = JsonConvert.DeserializeObject<Libx264>(j);
                                    _presets.Add(preset);
                                }
                                break;

                            case "libx265":
                                {
                                    var preset = JsonConvert.DeserializeObject<Libx265>(j);
                                    _presets.Add(preset);
                                }
                                break;

                            default:
                                {
                                    var preset = JsonConvert.DeserializeObject<FFmpegPreset>(j);
                                    _presets.Add(preset);
                                }
                                break;
                        }
                    }
                }
            }

            return _presets;
        }

        private void SavePresets()
        {
            var json = JsonConvert.SerializeObject(Presets, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(PresetsFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsFilePath));
            }

            File.WriteAllText(PresetsFilePath, json);
        }

        private Dictionary<CodecType, SortedDictionary<string, string>> GetAvailableCodecs()
        {
            var codecs = new Dictionary<CodecType, SortedDictionary<string, string>>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg");
            }

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
            var formats = new SortedDictionary<string, string>();
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                p.StartInfo.FileName = Path.Combine(ExecutablesPath, "ffmpeg");
            }

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
    }
}