using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace Compressarr.JobProcessing
{
    public class ProcessManager : IProcessManager
    {
        ILogger<ProcessManager> logger;
        public ProcessManager(ILogger<ProcessManager> logger)
        {
            this.logger = logger;

        }

        public Task Process(Job job)
        {
            logger.LogInformation("FFmpeg Process starting.");


            var succeded = true;

            return Task.Run(() =>
            {
                job.Process.cont = true;

                job.Process.WorkItem.Running = true;
                logger.LogDebug("Async FFmpeg Process workItem starting.");

                foreach (var arg in job.Process.WorkItem.Arguments)
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

                            converionTask.Wait();
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

                logger.LogDebug($"FFmpeg Process finished. Successful = {succeded}");

                job.Process.WorkItem.Success = succeded;
                job.Process.WorkItem.Finished = true;
                job.Process.WorkItem.Running = false;
                job.Process.Update(this);
            });
        }

        public void Stop(Job job)
        {
            if (job.Process != null)
            {
                job.Process.cont = false;
                job.Log("Job Stop requested", LogLevel.Information);
                if (job.Process.Converter != null)
                {
                    job.Process.cancellationTokenSource.Cancel();
                }
            }
            else
            {
                logger.LogWarning("Job process cannot be stopped, Process is null");
            }
        }

        private void Converter_OnProgress(ConversionProgressEventArgs args, FFmpegProcess process)
        {
            process.WorkItem.Duration = args.Duration;
            process.WorkItem.TotalLength = args.TotalLength;
            process.WorkItem.Percent = args.Percent;
            //workItem.UpdateStatus("");
            process.Update();// Log();
        }

        //private Regex reg = new Regex(@"\w+=\s*[\d\.\-:/A-Za-z]+(?=\s)");
        private Regex reg = new Regex(@"frame=\s*(\d*)\s*fps=\s*(\d*)\s*q=\s*(-?[\d\.]*)\s*size=\s*(\d*\wB)\s*time=\s*([\d:\.]*)\s*bitrate=\s*([\d\.]*\w*\/s)\s*speed=\s*([\d.]*x)\s*");
        private void Converter_OnDataReceived(DataReceivedEventArgs e, FFmpegProcess process)
        {
            //"frame= 2171 fps= 58 q=-0.0 size=    4396kB time=00:01:28.50 bitrate= 406.9kbits/s speed=2.38x    ";

            logger.LogDebug(e.Data);

            if (reg.Match(e.Data).Success)
            {
                var match = reg.Match(e.Data);


                if (long.TryParse(match.Groups[1].Value.Trim(), out var frame)) process.WorkItem.Frame = frame;
                if (int.TryParse(match.Groups[2].Value.Trim(), out var fps)) process.WorkItem.FPS = fps;
                if (decimal.TryParse(match.Groups[3].Value.Trim(), out var q)) process.WorkItem.Q = q;
                process.WorkItem.Size = match.Groups[4].Value.Trim();
                process.WorkItem.Bitrate = match.Groups[6].Value.Trim();
                process.WorkItem.Speed = match.Groups[7].Value.Trim();

                //foreach (var m in match.ToList())
                //{
                //    var mSplit = m.Value.Split("=");
                //    switch (mSplit[0].Trim())
                //    {
                //        case "frame":
                //            long frame;
                //            if (long.TryParse(mSplit[1].Trim(), out frame)) workItem.Frame = frame;
                //            break;

                //        case "fps":
                //            int fps;
                //            if (int.TryParse(mSplit[1].Trim(), out fps)) workItem.FPS = fps;
                //            break;

                //        case "q":
                //            decimal q;
                //            if (decimal.TryParse(mSplit[1].Trim(), out q)) workItem.Q = q;
                //            break;

                //        case "size":
                //            workItem.Size = mSplit[1].Trim();
                //            break;

                //        case "bitrate":
                //            workItem.Bitrate = mSplit[1].Trim();
                //            break;

                //        case "speed":
                //            workItem.Speed = mSplit[1].Trim();
                //            break;
                //    }
                //}
                process.Update();
            }
            else
            {
                logger.LogWarning($"Converter data not recognised: {e.Data}");
            }
        }
    }
}
