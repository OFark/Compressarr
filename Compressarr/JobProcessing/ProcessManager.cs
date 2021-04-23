using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace Compressarr.JobProcessing
{
    public class ProcessManager : IProcessManager
    {
        private readonly Regex progressReg = new(@"frame=\s*(\d*)\sfps=\s*([\d\.]*)\sq=\s*(-?[\d\.]*)\ssize=\s*([^\s]*)\stime=\s*([\d:\.]*)\sbitrate=\s*([^\s]*)\sspeed=\s*([\d.]*x)\s*");
        private readonly Regex ssimReg = new(@"\[Parsed_ssim_4\s@\s\w*\]\sSSIM\sY\:\d\.\d*\s\([inf\d\.]*\)\sU\:\d\.\d*\s\([inf\d\.]*\)\sV\:\d\.\d*\s\([inf\d\.]*\)\sAll\:(\d\.\d*)\s\([inf\d\.]*\)");
        private readonly ILogger<ProcessManager> logger;

        public ProcessManager(ILogger<ProcessManager> logger)
        {
            this.logger = logger;

        }

        public async Task Process(Job job)
        {
            using (logger.BeginScope("FFmpeg Process"))
            {
                logger.LogInformation("Starting.");

                var succeded = true;

                job.Process.cont = true;

                job.Process.WorkItem.Running = true;
                logger.LogDebug("FFmpeg process work item starting.");

                foreach (var arg in job.Preset.Arguments)
                {
                    if (job.Process.cont)
                    {
                        var arguments = string.Format(arg, job.Process.WorkItem.SourceFile, job.Process.WorkItem.DestinationFile);
                        logger.LogDebug($"FFmpeg Process arguments: \"{arguments}\"");

                        Task<IConversionResult> converionTask = null;
                        try
                        {
                            job.Process.Converter = FFmpeg.Conversions.New();
                            job.Process.Converter.OnDataReceived += (sender, args) => Converter_OnDataReceived(args, job.Process);
                            job.Process.Converter.OnProgress += (sender, args) => Converter_OnProgress(args, job.Process);

                            converionTask = job.Process.Converter.Start(arguments, job.Process.cancellationTokenSource.Token);
                            logger.LogDebug("FFmpeg conversion started.");
                            await converionTask;
                            logger.LogDebug("FFmpeg conversion finished.");
                        }
                        catch (Exception ex)
                        {
                            job.Process.Update(this);
                            job.Log(ex.Message, LogLevel.Error);
                            job.Log(ex.InnerException?.Message, LogLevel.Error);
                            succeded = false;
                        }
                        finally
                        {
                            if (!converionTask.IsCompletedSuccessfully)
                            {
                                logger.LogDebug("FFmpeg conversion not successful");
                                succeded = false;
                            }
                        }
                    }
                }

                logger.LogInformation($"FFmpeg Process finished. Successful = {succeded}");

                if (succeded)
                {
                    if (job.SSIMCheck)
                    {
                        job.Process.Update(this, "Calculating SSIM");

                        var arguments = $" -i \"{job.Process.WorkItem.SourceFile}\" -i \"{job.Process.WorkItem.DestinationFile}\" -lavfi  \"[0:v]settb = AVTB,setpts = PTS - STARTPTS[main];[1:v]settb = AVTB,setpts = PTS - STARTPTS[ref];[main][ref]ssim\" -f null -";
                        logger.LogDebug($"SSIM arguments: {arguments}");

                        job.Process.Converter = FFmpeg.Conversions.New();
                        job.Process.Converter.OnDataReceived += (sender, args) => Converter_OnDataReceived(args, job.Process);
                        job.Process.Converter.OnProgress += (sender, args) => Converter_OnProgress(args, job.Process);

                        try
                        {
                            logger.LogDebug("FFmpeg starting SSIM.");
                            await job.Process.Converter.Start(arguments, job.Process.cancellationTokenSource.Token);
                            logger.LogDebug("FFmpeg finished SSIM.");
                        }
                        catch(OperationCanceledException ocex)
                        {
                            job.Process.Update(this); 
                            job.Log(ocex.Message, LogLevel.Error);
                            job.Log(ocex.InnerException?.Message, LogLevel.Error);
                            succeded = false;
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

                job.Process.WorkItem.Success = succeded;
                job.Process.WorkItem.Finished = true;
                job.Process.WorkItem.Running = false;
                job.Process.Update(this);
            }
        }

        public void Stop(Job job)
        {
            using (logger.BeginScope("Stop Processing"))
            {
                if (job.Process != null)
                {
                    job.Process.cont = false;
                    job.Log("Job Stop requested", LogLevel.Information);
                    if (job.Process.Converter != null)
                    {
                        logger.LogDebug("Cancellation Token Set");
                        job.Process.cancellationTokenSource.Cancel();
                    }
                }
                else
                {
                    logger.LogWarning("Job process cannot be stopped, Process is null");
                }
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

                if (progressReg.TryMatch(e.Data, out var match))
                {
                    logger.LogTrace(e.Data);
                    if (long.TryParse(match.Groups[1].Value.Trim(), out var frame)) process.WorkItem.Frame = frame;
                    if (decimal.TryParse(match.Groups[2].Value.Trim(), out var fps)) process.WorkItem.FPS = fps;
                    if (decimal.TryParse(match.Groups[3].Value.Trim(), out var q)) process.WorkItem.Q = q;
                    process.WorkItem.Size = match.Groups[4].Value.Trim();
                    process.WorkItem.Bitrate = match.Groups[6].Value.Trim();
                    process.WorkItem.Speed = match.Groups[7].Value.Trim();
                    process.Update();
                }
                else if (ssimReg.TryMatch(e.Data, out var ssimMatch))
                {
                    logger.LogTrace(e.Data);
                    if (decimal.TryParse(ssimMatch.Groups[1].Value.Trim(), out var ssim)) process.WorkItem.SSIM = ssim;

                    logger.LogInformation($"SSIM: {process.WorkItem.SSIM}");
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

            process.Update();
        }
    }
}
