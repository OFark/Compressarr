using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Microsoft.Extensions.DependencyInjection;
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

namespace Compressarr.FFmpegFactory
{
    public class FFmpegProcess
    {
        private IConversion converter;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private bool cont = false;

        private WorkItem workItem;

        public event EventHandler OnUpdate;

        public bool Succeded;

        public string FileName;

        public Task Process(WorkItem workItem)
        {
            this.workItem = workItem;

            var succeded = true;

            return Task.Run(() =>
            {
                cont = true;

                workItem.Running = true;

                if (cont)
                {
                    foreach (var arg in workItem.Arguments)
                    {
                        var arguments = string.Format(arg, workItem.SourceFile, workItem.DestinationFile);

                        Task<IConversionResult> converionTask = null;
                        try
                        {
                            converter = FFmpeg.Conversions.New();
                            converter.OnDataReceived += Converter_OnDataReceived;
                            converter.OnProgress += Converter_OnProgress;
                            converionTask = converter.Start(arguments, cancellationTokenSource.Token);

                            converionTask.Wait();
                        }
                        catch (Exception ex)
                        {
                            OnUpdate?.Invoke(this, EventArgs.Empty);
                            Log(ex.Message, ex.InnerException?.Message);
                            succeded = false;
                        }
                        finally
                        {
                            if (!converionTask.IsCompletedSuccessfully)
                            {
                                succeded = false;
                            }
                        }
                    }
                }

                workItem.Success = succeded;
                workItem.Finished = true;
                workItem.Running = false;
                OnUpdate?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Stop()
        {
            cont = false;
            Log("Job Stop requested");
            if (converter != null)
            {
                cancellationTokenSource.Cancel();
            }
        }

        private void Converter_OnProgress(object sender, ConversionProgressEventArgs args)
        {
            workItem.Duration = args.Duration;
            workItem.TotalLength = args.TotalLength;
            workItem.Percent = args.Percent;
            workItem.UpdateStatus("");
        }

        private Regex reg = new Regex(@"\w+=\s*[\d\.\-:/A-Za-z]+(?=\s)");

        private void Converter_OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            //"frame= 2171 fps= 58 q=-0.0 size=    4396kB time=00:01:28.50 bitrate= 406.9kbits/s speed=2.38x    ";

            if (reg.Match(e.Data).Success)
            {
                var match = reg.Matches(e.Data);
                foreach (var m in match.ToList())
                {
                    var mSplit = m.Value.Split("=");
                    switch (mSplit[0].Trim())
                    {
                        case "frame":
                            long frame;
                            if (long.TryParse(mSplit[1].Trim(), out frame)) workItem.Frame = frame;
                            break;

                        case "fps":
                            int fps;
                            if (int.TryParse(mSplit[1].Trim(), out fps)) workItem.FPS = fps;
                            break;

                        case "q":
                            decimal q;
                            if (decimal.TryParse(mSplit[1].Trim(), out q)) workItem.Q = q;
                            break;

                        case "size":
                            workItem.Size = mSplit[1].Trim();
                            break;

                        case "bitrate":
                            workItem.Bitrate = mSplit[1].Trim();
                            break;

                        case "speed":
                            workItem.Speed = mSplit[1].Trim();
                            break;
                    }
                }
                Log();
            }
            else
            {
                Log(e.Data);
            }
        }

        private void Log(params string[] messages)
        {
            workItem.UpdateStatus(messages);
            OnUpdate?.Invoke(this, EventArgs.Empty);
        }
    }
}