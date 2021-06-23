﻿using Compressarr.Application;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace Compressarr.JobProcessing
{
    //Singleton
    public class ProcessManager : IProcessManager
    {
        private readonly IApplicationService applicationService;
        private readonly IFileService fileService;
        private readonly IHistoryService historyService;
        private readonly ILogger<ProcessManager> logger;

        readonly SemaphoreSlim semaphore = new(1, 1);
        bool tokenCanceled = false;
        public ProcessManager(IApplicationService applicationService, ILogger<ProcessManager> logger, IFileService fileService, IHistoryService historyService)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.historyService = historyService;
            this.logger = logger;

            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(fileService.GetAppDirPath(AppDir.FFmpeg));
        }

        public static Regex ProgressReg { get => new(@"frame= *(\d*) fps= *([\d\.]*) q=*(-?[\d\.]*)(?: q=*-?[\d\.]*)* size= *([^ ]*) time= *([\d:\.]*) bitrate= *([^ ]*) speed= *([\d.]*x) *"); }
        public static Regex SSIMReg { get => new(@"\[Parsed_ssim_4\s@\s\w*\]\sSSIM\sY\:\d\.\d*\s\([inf\d\.]*\)\sU\:\d\.\d*\s\([inf\d\.]*\)\sV\:\d\.\d*\s\([inf\d\.]*\)\sAll\:(\d\.\d*)\s\([inf\d\.]*\)"); }

        public async Task<SSIMResult> CalculateSSIM(DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string sourceFile, string destinationFile, string hardwareDecoder, CancellationToken token)
        {
            var arguments = $"{hardwareDecoder} -i \"{sourceFile}\" -i \"{destinationFile}\" -lavfi  \"[0:v]settb=AVTB,setpts=PTS-STARTPTS[main];[1:v]settb=AVTB,setpts=PTS-STARTPTS[ref];[main][ref]ssim\" -max_muxing_queue_size 2048 -f null -";
            logger.LogDebug($"SSIM arguments: {arguments}");

            decimal ssim = default;
            var converter = Xabe.FFmpeg.FFmpeg.Conversions.New();
            converter.OnDataReceived += (sender, e) =>
            {
                if (SSIMReg.TryMatch(e.Data, out var ssimMatch))
                {
                    logger.LogTrace(e.Data);
                    _ = decimal.TryParse(ssimMatch.Groups[1].Value.Trim(), out ssim);

                    logger.LogInformation($"SSIM: {ssim}");
                }
                dataRecieved?.Invoke(sender, e);
            };
            converter.OnProgress += dataProgress;

            try
            {
                logger.LogDebug("FFmpeg starting SSIM.");
                await converter.Start(arguments, token);
                logger.LogDebug("FFmpeg finished SSIM.");

                if (ssim == default)
                {
                    await Task.Delay(5000, token);
                }

                if (ssim != default)
                    return new(ssim);

                return new(false, ssim);
            }
            catch (OperationCanceledException)
            {
                return new(new OperationCanceledException("User cancelled SSIM check"));
            }
            catch (Exception ex)
            {
                return new(ex);
            }
        }

        public async Task<EncodingResult> EncodeAVideo(DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string arguments, CancellationToken token)
        {
            try
            {
                var converter = Xabe.FFmpeg.FFmpeg.Conversions.New();
                converter.OnDataReceived += dataRecieved;
                converter.OnProgress += dataProgress;

                var t = converter.Start(arguments, token);
                logger.LogDebug("FFmpeg conversion started.");
                await t;
                logger.LogDebug("FFmpeg conversion finished.");
                return new(true);
            }
            catch (OperationCanceledException)
            {
                return new(new OperationCanceledException("User cancelled encoding"));
            }
            catch (Exception ex)
            {
                return new(ex);
            }
        }

        public async Task GenerateSample(string sampleFile, WorkItem wi, FFmpegPreset preset, CancellationToken token)
        {

            using (logger.BeginScope("Generating Sample"))
            {
                var sampleListFile = Path.Combine(fileService.TempDir, $"sampleList.txt");

                if (File.Exists(sampleListFile)) File.Delete(sampleListFile);

                var sampleTime = applicationService.ArgCalcSampleSeconds;
                var videoLength = wi.Media?.MediaInfo?.format?.Duration.TotalSeconds ?? 3600;

                var samplePitch = videoLength / 4;

                var partialSampleFiles = new List<string>();

                for (int i = 1; i < 4; i++)
                {
                    var partialSampleFileName = Path.Combine(fileService.TempDir, $"sample{i}.{preset.ContainerExtension}");
                    partialSampleFiles.Add(partialSampleFileName);
                    var temparg = $"-y -ss {samplePitch * i} -t {sampleTime} -i \"{wi.SourceFile}\" -map 0 -codec copy -avoid_negative_ts 1 \"{partialSampleFileName}\"";

                    logger.LogInformation($"Generating Sample {i}  with: {temparg}");

                    var sampleEncodeResult = await EncodeAVideo(null, null, temparg, token);
                    if (!sampleEncodeResult.Success)
                    {
                        throw sampleEncodeResult.Exception ?? new("Not sure - something went wrong with encoding. Check the logs");
                    }
                    File.AppendAllLines(sampleListFile, new List<string>() { $"file '{partialSampleFileName}'" });
                }

                var concatArg = $"-y -f concat -safe 0 -i {sampleListFile} -map 0 -c copy {sampleFile}";

                logger.LogInformation($"Concatenating samples  with: {concatArg}");

                var encodeResult = await EncodeAVideo(null, null, concatArg, token);
                if (!encodeResult.Success)
                {
                    throw encodeResult.Exception ?? new("Not sure - something went wrong with encoding. Check the logs");
                }

                if (File.Exists(sampleListFile)) File.Delete(sampleListFile);
                foreach (var f in partialSampleFiles)
                {
                    if (File.Exists(f)) File.Delete(f);
                }
            }
        }

        public async Task Process(WorkItem workItem, CancellationToken token)
        {
            try
            {
                try
                {
                    await semaphore.WaitAsync(token);

                    using (var jobRunner = new JobWorker(workItem.Job.Condition.Encode, workItem.Job.UpdateStatus))
                    {

                        using (logger.BeginScope("FFmpeg Process"))
                        {
                            logger.LogInformation("Starting.");

                            var succeded = true;

                            
                            var historyID = historyService.StartProcessing(workItem.Media.UniqueID, workItem.SourceFile, workItem.Job.FilterName, workItem.Job.PresetName, workItem.Arguments);
                                                        
                            logger.LogDebug("FFmpeg process work item starting.");

                            foreach (var arg in workItem.Arguments)
                            {
                                if (succeded)
                                {
                                    logger.LogDebug($"FFmpeg Process arguments: \"{arg}\"");

                                    var result = await EncodeAVideo((sender, args) => Converter_OnDataReceived(args, workItem), (sender, args) => Converter_OnProgress(args, workItem), arg, token);

                                    if (!result.Success)
                                    {
                                        workItem.Update(result.Exception);
                                        succeded = false;
                                    }
                                }
                            }

                            logger.LogInformation($"FFmpeg Process finished. Successful = {succeded}");

                            if (succeded)
                            {
                                try
                                {
                                    var originalFileSize = new FileInfo(workItem.SourceFile).Length;
                                    var newFileSize = new FileInfo(workItem.DestinationFile).Length;

                                    workItem.Compression = newFileSize / (decimal)originalFileSize;
                                    workItem.Update();

                                }
                                catch (Exception ex) when (ex is FileNotFoundException || ex is IOException)
                                {
                                    workItem.Update(new Update(ex, LogLevel.Warning));
                                    logger.LogWarning(ex, "Error fetching file lengths");
                                    return;
                                }

                                if (workItem.Job.SSIMCheck || applicationService.AlwaysCalculateSSIM)
                                {
                                    workItem.Update("Calculating SSIM");
                                    var hardwareDecoder = workItem.Job.Preset.HardwareDecoder.Wrap("-hwaccel {0} ");

                                    var result = await CalculateSSIM((sender, args) => Converter_OnDataReceived(args, workItem), (sender, args) => Converter_OnProgress(args, workItem), workItem.SourceFile, workItem.DestinationFile, hardwareDecoder, token);

                                    succeded = result.Success;
                                    if(succeded)
                                    {
                                        workItem.SSIM = result.SSIM;
                                    }
                                    else
                                    {
                                        workItem.Update(result.Exception);
                                    }
                                }
                            }

                            historyService.EndProcessing(historyID, succeded, workItem);

                            jobRunner.Succeed();

                            workItem.Success = succeded;
                            workItem.Finished = true;
                            workItem.Update();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // The token was cancelled and the semaphore was NOT entered...
                    tokenCanceled = true;
                }
            }
            finally
            {
                if (!tokenCanceled)
                    semaphore.Release();
            }
        }
        private void Converter_OnDataReceived(DataReceivedEventArgs e, WorkItem wi)
        {
            //Conversion:
            //"frame= 2171 fps= 58 q=-0.0 size=    4396kB time=00:01:28.50 bitrate= 406.9kbits/s speed=2.38x    ";
            //"frame= 3097 fps= 22 q=-0.0 size=N/A time=00:02:04.72 bitrate=N/A speed=0.886x"
            //"frame=   81 fps=0.0 q=-0.0 size=N/A time=00:00:03.44 bitrate=N/A speed=6.69x"

            //SSIM:
            //"frame=16622 fps=2073 q=-0.0 size=N/A time=00:11:06.93 bitrate=N/A speed=83.2x"
            //"[Parsed_ssim_4 @ 000002aaab376c00] SSIM Y:0.995682 (23.646920) U:0.995202 (23.189259) V:0.995373 (23.347260) All:0.995550 (23.516743)"
            //"[Parsed_ssim_4 @ 000001d1588cd080] SSIM Y:1.000000 (inf) U:1.000000 (inf) V:1.000000 (inf) All:1.000000 (inf)"

            using (logger.BeginScope("Converter Data Received"))
            {

                if (ProgressReg.TryMatch(e.Data, out var match))
                {
                    wi.Output(new(e.Data, LogLevel.Debug), true);
                    logger.LogTrace(e.Data);
                    if (long.TryParse(match.Groups[1].Value.Trim(), out var frame)) wi.Frame = frame;
                    if (decimal.TryParse(match.Groups[2].Value.Trim(), out var fps)) wi.FPS = fps;
                    if (decimal.TryParse(match.Groups[3].Value.Trim(), out var q)) wi.Q = q;
                    wi.Size = match.Groups[4].Value.Trim();
                    wi.Bitrate = match.Groups[6].Value.Trim();
                    wi.Speed = match.Groups[7].Value.Trim();
                    wi.Update();
                }
                else if (SSIMReg.TryMatch(e.Data, out var ssimMatch))
                {
                    logger.LogInformation($"SSIM: {ssimMatch}");
                    wi.Output(new($"SSIM: {ssimMatch}"));
                }
                else
                {
                    wi.Output(new(e.Data, LogLevel.Debug), false);
                    logger.LogTrace($"Converter data not recognised: {e.Data}");
                }
            }
        }

        private void Converter_OnProgress(ConversionProgressEventArgs args, WorkItem wi)
        {
            //Here Duration is the current time frame;
            wi.EncodingDuration = args.Duration;
            wi.Percent = args.Percent;

            // todo: If size looks to be over original option to abandon

            wi.Update();
        }
    }
}
