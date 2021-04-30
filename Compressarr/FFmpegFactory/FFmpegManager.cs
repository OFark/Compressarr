using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory
{
    public class FFmpegManager : IFFmpegManager
    {

        private readonly ILogger<FFmpegManager> logger;
        private readonly ISettingsManager settingsManager;
        public FFmpegManager(ILogger<FFmpegManager> logger, ISettingsManager settingsManager)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;

            FFmpeg.SetExecutablesPath(SettingsManager.GetAppDirPath(AppDir.FFmpeg));
        }

        public SortedDictionary<string, string> AudioCodecs => Codecs[CodecType.Audio];
        private Dictionary<CodecType, SortedDictionary<string, string>> Codecs => settingsManager.Codecs;
        public SortedDictionary<string, string> Containers => settingsManager.Containers;
        public HashSet<FFmpegPreset> Presets => settingsManager.Presets;

        public SortedDictionary<string, string> SubtitleCodecs => Codecs[CodecType.Subtitle];
        public SortedDictionary<string, string> VideoCodecs => Codecs[CodecType.Video];
        internal static string FFMPEG => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SettingsManager.GetFilePath(AppDir.FFmpeg, "ffmpeg.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SettingsManager.GetFilePath(AppDir.FFmpeg, "ffmpeg")
                               : throw new NotSupportedException("Cannot Identify OS");

        internal static string FFPROBE => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SettingsManager.GetFilePath(AppDir.FFmpeg, "ffprobe.exe")
                               : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SettingsManager.GetFilePath(AppDir.FFmpeg, "ffprobe")
                               : throw new NotSupportedException("Cannot Identify OS");



        public async Task AddPresetAsync(FFmpegPreset newPreset)
        {
            using (logger.BeginScope("Adding Preset"))
            {
                logger.LogInformation($"Preset Name: {newPreset.Name}");

                var preset = Presets.FirstOrDefault(x => x.Name == newPreset.Name);

                if (preset == null)
                {
                    logger.LogDebug("Adding a new preset.");
                    Presets.Add(newPreset);
                }
                else
                {
                    logger.LogDebug("Preset already exists, removing the old one.");
                    Presets.Remove(preset);
                    logger.LogDebug("Adding the new one.");
                    Presets.Add(newPreset);
                }

                await settingsManager.SaveAppSetting();
            }
        }

        public async Task<WorkItemCheckResult> CheckResult(Job job)
        {

            using (logger.BeginScope("Checking Results."))
            {
                if (job?.Process?.WorkItem == null)
                {
                    return null;
                }
                var result = new WorkItemCheckResult(job.Process.WorkItem);

                var mediaInfo = await GetMediaInfoAsync(job.Process.WorkItem.DestinationFile);
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

            //todo wait for initialisation
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

        public async Task DeletePresetAsync(string presetName)
        {

            using (logger.BeginScope("Deleting Preset"))
            {
                logger.LogInformation($"Preset Name: {presetName}");

                var preset = Presets.FirstOrDefault(x => x.Name == presetName);

                if (preset != null)
                {
                    Presets.Remove(preset);
                }
                else
                {
                    logger.LogWarning($"Preset {presetName} not found.");
                }

                await settingsManager.SaveAppSetting();
            }
        }



        public async Task<IMediaInfo> GetMediaInfoAsync(string filepath)
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

        public async Task<HashSet<CodecOptionValue>> GetOptionsAsync(string codec)
        {
            using (logger.BeginScope("Get Codec Options"))
            {
                var optionsFile = SettingsManager.GetFilePath(AppDir.CodecOptions, $"{codec}.json");

                if (SettingsManager.HasFile(optionsFile))
                {
                    return await settingsManager.ReadJsonFileAsync<HashSet<CodecOptionValue>>(optionsFile);
                }
                else
                {
                    logger.LogDebug($"Codec Options file not found.");
                }
                return new();
            }
        }

        public FFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public async Task InitialisePreset(FFmpegPreset preset)
        {
            using (logger.BeginScope("Load Presets"))
            {
                if (preset.VideoCodecOptions != null && preset.VideoCodecOptions.Any())
                {
                    var codecOptions = await GetOptionsAsync(preset.VideoCodec);
                    if (codecOptions != null)
                    {
                        foreach (var co in codecOptions)
                        {
                            var val = preset.VideoCodecOptions.FirstOrDefault(x => x.Name == co.Name);
                            if (val != null)
                            {
                                co.Value = val.Value;
                            }
                        }
                    }

                    preset.VideoCodecOptions = codecOptions;
                }
            }
        }
    }
}