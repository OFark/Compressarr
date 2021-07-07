using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.FFmpeg.Events;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Humanizer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg.Events;
using static Compressarr.FFmpeg.FFmpegProcessor;

namespace Compressarr.JobProcessing
{
    //Singleton
    public class ProcessManager : IProcessManager
    {
        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly IHistoryService historyService;
        private readonly ILogger<ProcessManager> logger;

        readonly SemaphoreSlim semaphore = new(1, 1);
        bool tokenCanceled = false;
        public ProcessManager(IApplicationService applicationService, ILogger<ProcessManager> logger, IFFmpegProcessor fFmpegProcessor, IFileService fileService, IHistoryService historyService)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.historyService = historyService;
            this.logger = logger;

            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(fileService.GetAppDirPath(AppDir.FFmpeg));
        }

        public static Regex ProgressReg { get => new(@"frame= *(\d*) fps= *([\d\.]*) q=*(-?[\d\.]*)(?: q=*-?[\d\.]*)* size= *([^ ]*) time= *([\d:\.]*) bitrate= *([^ ]*) speed= *([\d.]*x) *"); }
        public static Regex SSIMReg { get => new(@"\[Parsed_ssim_4\s@\s\w*\]\sSSIM\sY\:\d\.\d*\s\([inf\d\.]*\)\sU\:\d\.\d*\s\([inf\d\.]*\)\sV\:\d\.\d*\s\([inf\d\.]*\)\sAll\:(\d\.\d*)\s\([inf\d\.]*\)"); }

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

        public async Task GenerateSamples(List<string> sampleFiles, WorkItem wi, CancellationToken token)
        {

            using (logger.BeginScope("Generating Sample"))
            {
                //var sampleListFile = Path.Combine(fileService.TempDir, $"sampleList.txt");

                //if (File.Exists(sampleListFile)) File.Delete(sampleListFile);

                var sampleTime = wi.Job.ArgumentCalculationSettings.ArgCalcSampleSeconds;
                var videoLength = wi.Media?.FFProbeMediaInfo?.format?.Duration.TotalSeconds ?? 3600;

                var samplePitch = videoLength / (sampleFiles.Count + 1);

                for (int i = 0; i < sampleFiles.Count; i++)
                {
                    var sampleFile = Path.GetFileName(sampleFiles[i]);
                    var partialSampleFileName = Path.Combine(fileService.TempDir, sampleFile);
                    //sampleFiles.Add(partialSampleFileName);
                    var temparg = $"-y -ss {samplePitch * (i + 1)} -t {sampleTime} -i \"{wi.SourceFile}\" -map 0 -codec copy \"{partialSampleFileName}\"";

                    logger.LogInformation($"Generating Sample {i}  with: {temparg}");

                    var sampleEncodeResult = await EncodeAVideo(null, null, temparg, token);
                    if (!sampleEncodeResult.Success)
                    {
                        throw sampleEncodeResult.Exception ?? new("Not sure - something went wrong with encoding. Check the logs");
                    }
                    //File.AppendAllLines(sampleListFile, new List<string>() { $"file '{partialSampleFileName}'" });
                }

                //var concatArg = $"-y -f concat -safe 0 -i {sampleListFile} -map 0 -c copy {sampleFile}";

                //logger.LogInformation($"Concatenating samples  with: {concatArg}");

                //var encodeResult = await EncodeAVideo(null, null, concatArg, token);
                //if (!encodeResult.Success)
                //{
                //    throw encodeResult.Exception ?? new("Not sure - something went wrong with encoding. Check the logs");
                //}

                //if (File.Exists(sampleListFile)) File.Delete(sampleListFile);
                //foreach (var f in partialSampleFiles)
                //{
                //    if (File.Exists(f)) File.Delete(f);
                //}
            }
        }

        public async Task<bool> Process(WorkItem workItem, CancellationToken token)
        {
            try
            {

                await semaphore.WaitAsync(token);

                using (logger.BeginScope("FFmpeg Process"))
                {
                    logger.LogInformation("Starting.");

                    logger.LogDebug("FFmpeg process work item starting.");

                    foreach (var arg in workItem.Arguments)
                    {

                        logger.LogDebug($"FFmpeg Process arguments: \"{arg}\"");

                        var result = await EncodeAVideo((sender, args) => Converter_OnDataReceived(args, workItem), (sender, args) => Converter_OnProgress(args, workItem), arg, token);

                        if (!result.Success)
                        {
                            workItem.Update(result.Exception);
                            logger.LogInformation($"FFmpeg Process failed.", result.Exception);
                            return false;
                        }

                    }

                    logger.LogInformation($"FFmpeg Process finished.");
                    return true;
                }

            }
            catch (OperationCanceledException)
            {
                // The token was cancelled and the semaphore was NOT entered...
                tokenCanceled = true;
            }

            finally
            {
                if (!tokenCanceled)
                    semaphore.Release();
            }

            return false;
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

                if (FFmpegProgress.TryParse(e.Data, out var progress))
                {
                    wi.Output(new(e.Data, LogLevel.Debug), true);
                    logger.LogTrace(e.Data);
                    wi.Frame = progress.Frame;
                    wi.FPS = progress.FPS;
                    wi.Q = progress.Q;
                    wi.Size = progress.Size.Bytes().Humanize("0.00");
                    wi.Bitrate = progress.Bitrate;
                    wi.Speed = progress.Speed;
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
