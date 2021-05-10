using Compressarr.FFmpegFactory.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
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
    public class FFmpegManager : FFmpegReliant, IFFmpegManager
    {

        private readonly ILogger<FFmpegManager> logger;
        private readonly ISettingsManager settingsManager;
        public FFmpegManager(ILogger<FFmpegManager> logger, ISettingsManager settingsManager, IFFmpegInitialiser fFmpegInitialiser)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;

            FFmpeg.SetExecutablesPath(SettingsManager.GetAppDirPath(AppDir.FFmpeg));

            WhenReady(fFmpegInitialiser, () => InitialisePresets());
        }

        public SortedSet<Codec> AudioCodecs => Codecs[CodecType.Audio];
        private Dictionary<CodecType, SortedSet<Codec>> Codecs => settingsManager.Codecs;
        public SortedDictionary<string, string> Containers => settingsManager.Containers;
        public HashSet<FFmpegPreset> Presets => settingsManager.Presets;

        public SortedSet<Codec> SubtitleCodecs => Codecs[CodecType.Subtitle];
        public SortedSet<Codec> VideoCodecs => Codecs[CodecType.Video];
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

                if (Presets.Contains(newPreset))
                {
                    logger.LogDebug("Preset already exists, updating.");
                }
                else
                {
                    logger.LogDebug("Adding a new preset.");
                    newPreset.Initialised = true;
                    settingsManager.Presets.Add(newPreset);
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

                if(container == "copy")
                {
                    return null;
                }

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

        public async Task DeletePresetAsync(FFmpegPreset preset)
        {

            using (logger.BeginScope("Deleting Preset: {preset}", preset))
            {
                if (Presets.Contains(preset))
                {
                    logger.LogInformation($"Removing");
                    Presets.Remove(preset);
                    settingsManager.Presets.Remove(preset);
                }
                else
                {
                    logger.LogWarning($"Preset {preset.Name} not found.");
                }

                await settingsManager.SaveAppSetting();
            }
        }

        public Codec GetCodec(CodecType type, string name)
        {
            if (Ready || ReadyEvent.WaitOne(new TimeSpan(0, 1, 0)))
            {

                var codec = Codecs[type].FirstOrDefault(c => c.Name == name);

                return codec ?? new(); //New will be "Copy"
            }
            throw new TimeoutException("FFmpeg not ready in time");
        }

        public async Task<IMediaInfo> GetMediaInfoAsync(string filepath)
        {
            if (Ready || ReadyEvent.WaitOne(new TimeSpan(0, 1, 0)))
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

            throw new TimeoutException("FFmpeg not ready in time");
        }

        public FFmpegPreset GetPreset(string presetName) => Presets.FirstOrDefault(p => p.Name == presetName);

        public Task<StatusResult> GetStatus()
        {
            return Task.Run(() =>
            {
                return new StatusResult()
                {
                    Status = Presets.Any() ? ServiceStatus.Ready : ServiceStatus.Incomplete,
                    Message = new(Presets.Any() ? "Ready" : "No presets have been defined, you can create some on the <a href=\"/ffmpeg\">FFmpeg</a> page")
                };
            });
        }

        private void InitialisePresets()
        {
            using (logger.BeginScope("Initialising Presets"))
            {
                if (Presets != null)
                {
                    foreach (var preset in Presets.Where(p => !p.Initialised))
                    {
                        var codec = GetCodec(CodecType.Video, preset.VideoCodec.Name);
                        if (codec != null)
                        {
                            preset.VideoCodec = codec;
                        }

                        preset.Initialised = true;
                    }
                }
            }
        }
    }
}