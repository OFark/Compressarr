using Compressarr.Application;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
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
        private readonly ILogger<ProcessManager> logger;

        readonly SemaphoreSlim semaphore = new(1, 1);
        bool tokenCanceled = false;
        public ProcessManager(IApplicationService applicationService, ILogger<ProcessManager> logger, IFileService fileService)
        {
            this.applicationService = applicationService;
            this.logger = logger;

            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(fileService.GetAppDirPath(AppDir.FFmpeg));
        }

        public Regex ProgressReg { get => new(@"frame= *(\d*) fps= *([\d\.]*) q=*(-?[\d\.]*)(?: q=*-?[\d\.]*)* size= *([^ ]*) time= *([\d:\.]*) bitrate= *([^ ]*) speed= *([\d.]*x) *"); }
        public Regex SSIMReg { get => new(@"\[Parsed_ssim_4\s@\s\w*\]\sSSIM\sY\:\d\.\d*\s\([inf\d\.]*\)\sU\:\d\.\d*\s\([inf\d\.]*\)\sV\:\d\.\d*\s\([inf\d\.]*\)\sAll\:(\d\.\d*)\s\([inf\d\.]*\)"); }

        public async Task<SSIMResult> CalculateSSIM(IConversion converter, DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string sourceFile, string destinationFile, CancellationToken token)
        {
            var arguments = $" -i \"{sourceFile}\" -i \"{destinationFile}\" -lavfi  \"[0:v]settb = AVTB,setpts = PTS - STARTPTS[main];[1:v]settb = AVTB,setpts = PTS - STARTPTS[ref];[main][ref]ssim\" -f null -";
            logger.LogDebug($"SSIM arguments: {arguments}");

            decimal ssim = default;
            converter = Xabe.FFmpeg.FFmpeg.Conversions.New();
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

        public async Task<EncodingResult> EncodeAVideo(IConversion converter, DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string arguments, CancellationToken token)
        {
            try
            {
                converter = Xabe.FFmpeg.FFmpeg.Conversions.New();
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

        public async Task Process(Job job)
        {
            try
            {
                try
                {
                    await semaphore.WaitAsync(job.Process.cancellationTokenSource.Token);

                    using (var jobRunner = new JobWorker(job.Condition.Encode, job.UpdateStatus))
                    {

                        using (logger.BeginScope("FFmpeg Process"))
                        {
                            logger.LogInformation("Starting.");

                            var succeded = true;

                            job.Process.cont = true;

                            job.Process.WorkItem.Running = true;
                            logger.LogDebug("FFmpeg process work item starting.");

                            foreach (var arg in job.Process.WorkItem.Arguments)
                            {
                                if (job.Process.cont && succeded)
                                {
                                    var arguments = string.Format(arg, job.Process.WorkItem.SourceFile, job.Process.WorkItem.DestinationFile);
                                    logger.LogDebug($"FFmpeg Process arguments: \"{arguments}\"");

                                    var result = await EncodeAVideo(job.Process.Converter, (sender, args) => Converter_OnDataReceived(args, job.Process), (sender, args) => Converter_OnProgress(args, job.Process), arguments, job.Process.cancellationTokenSource.Token);

                                    if (!result.Success)
                                    {
                                        job.Process.WorkItem.Update();
                                        job.Log(result.Exception.ToString(), LogLevel.Error);
                                        job.Log(result.Exception.InnerException?.Message, LogLevel.Error);
                                        succeded = false;
                                    }
                                }
                            }

                            logger.LogInformation($"FFmpeg Process finished. Successful = {succeded}");

                            if (succeded)
                            {
                                if (job.SSIMCheck || applicationService.AlwaysCalculateSSIM)
                                {
                                    job.Process.WorkItem.Update("Calculating SSIM");

                                    var result = await CalculateSSIM(job.Process.Converter, (sender, args) => Converter_OnDataReceived(args, job.Process), (sender, args) => Converter_OnProgress(args, job.Process), job.Process.WorkItem.SourceFile, job.Process.WorkItem.DestinationFile, job.Process.cancellationTokenSource.Token);

                                    succeded = result.Success;
                                    if(succeded)
                                    {
                                        job.Process.WorkItem.SSIM = result.SSIM;
                                    }
                                    else
                                    {
                                        job.Process.WorkItem.Update();
                                        job.Log(result.Exception.Message, LogLevel.Error);
                                        job.Log(result.Exception.InnerException?.Message, LogLevel.Error);
                                    }
                                }

                                if (succeded)
                                {
                                    try
                                    {
                                        var originalFileSize = new FileInfo(job.Process.WorkItem.SourceFile).Length;
                                        var newFileSize = new FileInfo(job.Process.WorkItem.DestinationFile).Length;

                                        job.Process.WorkItem.Compression = newFileSize / (decimal)originalFileSize;

                                    }
                                    catch (Exception ex) when (ex is FileNotFoundException || ex is IOException)
                                    {
                                        job.Process.WorkItem.Compression = null;
                                        logger.LogWarning(ex, "Error fetching file lengths");
                                    }
                                }
                            }

                            jobRunner.Succeed();

                            job.Process.WorkItem.Success = succeded;
                            job.Process.WorkItem.Finished = true;
                            job.Process.WorkItem.Running = false;
                            job.Process.WorkItem.Update();
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
        private void Converter_OnDataReceived(DataReceivedEventArgs e, FFmpegProcess process)
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
                process.Output(e.Data, LogLevel.Debug);

                if (ProgressReg.TryMatch(e.Data, out var match))
                {
                    logger.LogTrace(e.Data);
                    if (long.TryParse(match.Groups[1].Value.Trim(), out var frame)) process.WorkItem.Frame = frame;
                    if (decimal.TryParse(match.Groups[2].Value.Trim(), out var fps)) process.WorkItem.FPS = fps;
                    if (decimal.TryParse(match.Groups[3].Value.Trim(), out var q)) process.WorkItem.Q = q;
                    process.WorkItem.Size = match.Groups[4].Value.Trim();
                    process.WorkItem.Bitrate = match.Groups[6].Value.Trim();
                    process.WorkItem.Speed = match.Groups[7].Value.Trim();
                    process.WorkItem.Update();
                }
                else if (SSIMReg.TryMatch(e.Data, out var ssimMatch))
                {
                    logger.LogInformation($"SSIM: {ssimMatch}");
                }
                else
                {
                    logger.LogTrace($"Converter data not recognised: {e.Data}");
                }
            }
        }

        private void Converter_OnProgress(ConversionProgressEventArgs args, FFmpegProcess process)
        {
            //Here Duration is the current time frame;
            process.WorkItem.Duration = args.Duration;
            process.WorkItem.Percent = args.Percent;

            // todo: If size looks to be over original option to abandon

            process.WorkItem.Update();
        }
    }
}
