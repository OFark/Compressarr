﻿using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.FFmpeg.Events;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Settings;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Compressarr.FFmpeg.FFmpegProcessor;

namespace Compressarr.JobProcessing
{
    public class ArgumentService : IArgumentService
    {
        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly IHistoryService historyService;
        private readonly ILogger<ArgumentService> logger;
        private readonly IMediaInfoService mediaInfoService;
        private readonly IProcessManager processManager;
        public ArgumentService(IApplicationService applicationService, IFFmpegProcessor fFmpegProcessor, IFileService fileService, IHistoryService historyService, ILogger<ArgumentService> logger, IMediaInfoService mediaInfoService, IProcessManager processManager)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.historyService = historyService;
            this.logger = logger;
            this.mediaInfoService = mediaInfoService;
            this.processManager = processManager;
        }

        public List<string> GetArguments(WorkItem wi) => GetArguments(wi, wi.SourceFile, wi.DestinationFile);

        public List<string> GetArguments(WorkItem wi, string sourceFile, string destinationFile) => GetArgumentTemplates(wi)?.Select(arg => string.Format(arg, sourceFile, destinationFile)).ToList();

        public List<string> GetArgumentTemplates(WorkItem wi)
        {
            if (wi.ArgumentCalculator == null) return null;

            List<string> args = new();

            if (wi.ArgumentCalculator.TwoPass)
            {
                args.Add(GetArgument(wi.ArgumentCalculator, 1));
                args.Add(GetArgument(wi.ArgumentCalculator, 2));
            }
            else
            {
                args.Add(GetArgument(wi.ArgumentCalculator));
            }

            return args;
        }

        public async Task<string> SetArguments(FFmpegPreset preset, WorkItem wi, CancellationToken token, bool force = false)
        {

            using (logger.BeginScope("Set Arguments"))
            {
                if (wi.Arguments == null || force)
                {
                    await applicationService.InitialisePresets;

                    if (wi.Media?.FFProbeMediaInfo == null)
                    {
                        var ffProbeResponse = await mediaInfoService.GetMediaInfo(wi.Media, token);
                        if (ffProbeResponse.Success)
                        {
                            wi.Media.FFProbeMediaInfo = ffProbeResponse.Result;
                        }
                        else
                        {
                            return ffProbeResponse.ErrorMessage;
                        }
                    }

                    wi.ArgumentCalculator = new ArgumentCalculator(wi, preset);

                    await CalculateBestOptions(wi, token);

                    if (token.IsCancellationRequested) return "Process cancelled";

                    wi.Arguments = GetArguments(wi);
                }

                return null;
            }
        }

        private async Task CalculateBestOptions(WorkItem wi, CancellationToken token)
        {
            using (logger.BeginScope("Calculating Best Encoder Options"))
            {
                var preset = wi.ArgumentCalculator.Preset;

                Task genSampleTask = null;

                if (wi.ArgumentCalculator.SampleSize == 0)
                {
                    wi.ArgumentCalculator.SampleSize = 4; // new Random().Next(1, 5);
                }
                var sampleFiles = Enumerable.Range(1, wi.ArgumentCalculator.SampleSize).Select(x => Path.Combine(fileService.TempDir, $"sample{x}{wi.SourceFileExtension}")).ToList();


                var hardwareDecoder = preset.HardwareDecoder.Wrap("-hwaccel {0} ");
                var frameRate = preset.FrameRate.HasValue ? $" -r {preset.FrameRate}" : "";
                var optionalArguments = preset.OptionalArguments;

                wi.Output("Calculating Best Encoder Options");
                if (token.IsCancellationRequested) return;

                foreach (var veo in wi.ArgumentCalculator.AutoCalcVideoEncoderOptions)
                {
                    if (veo?.EncoderOption == null) throw new InvalidDataException("Options no longer available, please rebuild the FFmpeg Preset");

                    var option = veo.EncoderOption;

                    var range = new List<string>();
                    switch (option.Type)
                    {
                        case CodecOptionType.Range:
                        case CodecOptionType.Number:
                            {
                                range = Enumerable.Range(Math.Min(option.AutoTune.Start, option.AutoTune.End), Math.Abs(option.AutoTune.End - option.AutoTune.Start) + 1).OrderBy(x => Math.Abs(x - option.AutoTune.Start)).Select(i => i.ToString()).ToList();
                            }
                            break;
                        case CodecOptionType.Select:
                        case CodecOptionType.String:
                            {
                                range = option.AutoTune.Values;
                            }
                            break;
                    }

                    veo.AutoPresetTests = new();

                    //set it up first so we can show the progress on the page
                    veo.AutoPresetTests.Add(new AutoPresetResult());
                    foreach (var key in range)
                    {
                        veo.AutoPresetTests.Add(new AutoPresetResult() { ArgumentValue = key });
                    }
                    wi.Update();
                }


                do
                {
                    foreach (var veo in wi.ArgumentCalculator.AutoCalcVideoEncoderOptions)
                    {
                        var prevVal = veo.Value;

                        veo.Value = null;

                        var currArg = GetArgument(wi.ArgumentCalculator);
                        veo.ArgumentHistory ??= new();

                        veo.HasSettled = veo.ArgumentHistory.Contains(currArg);

                        if (!veo.HasSettled) veo.ArgumentHistory.Add(currArg);
                        // If it is in the history then the next steps should auto fill from cache

                        foreach (var best in veo.AutoPresetTests.Where(x => x.Best)) { best.Best = false; }

                        foreach (var test in veo.AutoPresetTests)
                        {
                            veo.Value = test.ArgumentValue;

                            test.Reset();
                            test.Processing = true;

                            List<decimal> compressions = new();
                            List<decimal> speeds = new();
                            List<decimal> ssims = new();

                            foreach (var sampleFile in sampleFiles)
                            {
                                var sampleFileIndex = sampleFiles.IndexOf(sampleFile);

                                var tempEncFile = Path.Combine(fileService.TempDir, $"encFile.{preset.ContainerExtension}");

                                if (token.IsCancellationRequested) return;

                                wi.Update();

                                var arg = string.Format(GetArgument(wi.ArgumentCalculator), sampleFile, tempEncFile);

                                logger.LogInformation($"Trying: {arg}");

                                var storedResult = await historyService.GetAutoCalcResult(wi.Media.FFProbeMediaInfo.GetStableHash(), arg, wi.Job.ArgumentCalculationSettings.ArgCalcSampleSeconds);

                                if (storedResult != null && (!(wi.Job.ArgumentCalculationSettings.AutoCalculationType is AutoCalcType.BySpeed or AutoCalcType.Balanced or AutoCalcType.WeightedForCompression or AutoCalcType.WeightedForSpeed or AutoCalcType.WeightedForSSIM) || storedResult.Speed > 0))
                                {
                                    compressions.Add((decimal)storedResult.Size / storedResult.OriginalSize);
                                    speeds.Add(storedResult.Speed);
                                    ssims.Add(storedResult.SSIM);

                                    //test.AddSize(storedResult.Size, storedResult.OriginalSize);
                                    //test.SSIM = storedResult.SSIM;
                                    //test.Speed = storedResult.Speed;
                                    logger.LogDebug($"SSIM: {storedResult.SSIM}, Size: {storedResult.Size.ToFileSize()} {Math.Round((decimal)storedResult.Size / storedResult.OriginalSize * 100M, 2).Adorn("%")}");
                                }
                                else
                                {
                                    genSampleTask ??= processManager.GenerateSamples(sampleFiles, wi, token);
                                    await genSampleTask;

                                    var origSize = new FileInfo(sampleFile).Length;

                                    logger.LogInformation($"Testing sample encode with: {arg}");

                                    decimal tSpeed = 0;
                                    await processManager.EncodeAVideo((sender, args) =>
                                    {
                                        if (FFmpegProgress.TryParse(args.Data, out var progress)) test.Speed = tSpeed = progress.Speed;
                                    }
                                    , (sender, args) =>
                                    {
                                        test.EncodingProgress = (args.Percent / sampleFiles.Count) + (100D / sampleFiles.Count * sampleFileIndex);
                                        wi.Update();
                                    }, arg, token);

                                    test.EncodingProgress = 100D / sampleFiles.Count * (sampleFileIndex + 1);

                                    speeds.Add(tSpeed);

                                    if (token.IsCancellationRequested) return;

                                    var size = new FileInfo(tempEncFile).Length;
                                    compressions.Add((decimal)size / origSize);
                                    wi.Update();

                                    var ssimResult = await fFmpegProcessor.CalculateSSIM((args) =>
                                    {
                                        test.SSIMProgress = (args.Percentage / sampleFiles.Count) + (100D / sampleFiles.Count * sampleFileIndex);
                                        wi.Update();
                                    }, sampleFile, tempEncFile, hardwareDecoder, token);

                                    test.SSIMProgress = 100D / sampleFiles.Count * (sampleFileIndex + 1);

                                    if (ssimResult.Success)
                                    {
                                        ssims.Add(ssimResult.SSIM);
                                        logger.LogDebug($"SSIM: {ssimResult.SSIM}, Size: {size.ToFileSize()} {Math.Round((decimal)size / origSize * 100M, 2).Adorn("%")}");
                                        StoreAutoCalcResults(wi.Media.UniqueID, wi.Media.FFProbeMediaInfo.GetStableHash(), arg, ssimResult.SSIM, size, origSize, tSpeed, wi.Job.ArgumentCalculationSettings.ArgCalcSampleSeconds);
                                    }
                                }

                                if (File.Exists(tempEncFile)) File.Delete(tempEncFile);
                            }

                            test.Compression = compressions.Average();
                            test.Speed = speeds.Max();
                            test.SSIM = ssims.Average();



                            test.Processing = false;
                            wi.Update();
                        }

                        AutoPresetResult bestVal = default;

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.Balanced)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Smaller);
                            bestVal = BalanceResults(validVals);
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.WeightedForCompression)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Smaller);
                            bestVal = BalanceResults(validVals, weightCompression: 2);
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.WeightedForSpeed)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Smaller);
                            bestVal = BalanceResults(validVals, weightSpeed: 2);
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.WeightedForSSIM)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Smaller);
                            bestVal = BalanceResults(validVals, weightSSIM: 2);
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.BySpeed)
                        {
                            bestVal = veo.AutoPresetTests.OrderBy(x => Math.Abs(1 - x.Speed)).ThenByDescending(x => x.Speed > 1).ThenByDescending(x => x.SSIM).FirstOrDefault();
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.BangForBuck)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Smaller);

                            var minSSIM = validVals.Min(x => x.SSIM);
                            var devSSIM = validVals.Max(x => x.SSIM) - minSSIM;

                            var minComp = validVals.Min(x => x.Compression);
                            var devComp = validVals.Max(x => x.Compression) - minComp;

                            // clear out any outliers, sometimes FFmpeg goes way off kilter with one value that can skew the result set.
                            while (devSSIM != 0 && devComp != 0 && validVals.Select(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Compression - minComp) / (decimal)devComp)).Any(y => y == -1) && validVals.Count() > 2)
                            {
                                validVals = validVals.Where(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Compression - minComp) / (decimal)devComp) > -1);
                                minSSIM = validVals.Min(x => x.SSIM);
                                devSSIM = validVals.Max(x => x.SSIM) - minSSIM;

                                minComp = validVals.Min(x => x.Compression);
                                devComp = validVals.Max(x => x.Compression) - minComp;
                            }

                            if (devSSIM == 0)
                            {
                                bestVal = validVals.OrderBy(x => x.Compression).FirstOrDefault();
                            }
                            else if (devComp == 0)
                            {
                                bestVal = validVals.OrderByDescending(x => x.SSIM).FirstOrDefault();
                            }
                            else
                            {
                                bestVal = validVals.OrderByDescending(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Compression - minComp) / (decimal)devComp)).FirstOrDefault();
                            }
                        }

                        if (wi.Job.ArgumentCalculationSettings.AutoCalculationType == AutoCalcType.FirstPastThePost)
                        {
                            bestVal = veo.AutoPresetTests.Where(x => x != null && x.SSIM >= (wi.Job.ArgumentCalculationSettings.AutoCalculationPost ?? 99)).OrderBy(x => x.Compression).ThenByDescending(x => x.SSIM).FirstOrDefault();
                        }

                        if (bestVal == null || bestVal.Equals(default(KeyValuePair<string, AutoPresetResult>)))
                        {
                            bestVal = veo.AutoPresetTests.Where(x => x != null && x.Smaller).OrderByDescending(x => x.SSIM).ThenBy(x => x.Compression).FirstOrDefault();
                        };

                        if (bestVal == null || bestVal.Equals(default(KeyValuePair<string, AutoPresetResult>)))
                        {
                            bestVal = veo.AutoPresetTests.OrderBy(x => x.Compression).FirstOrDefault();
                        }

                        bestVal.Best = true;

                        var resultLog = veo.AutoPresetTests.Select(d => $"{d.ArgumentValue} | {d.SSIM} | {d.Compression.Adorn("%")} | {d.Speed.Adorn(" FPS")} {(d.Best ? "<=" : "")}");
                        logger.LogDebug($"SSIM results:\r\n{string.Join("\r\n", resultLog)}");


                        wi.Output($"Best Argument: {veo.EncoderOption.Arg.Replace("<val>", bestVal.ArgumentValue)}");
                        veo.Value = bestVal.ArgumentValue;
                        veo.HasSettled = veo.HasSettled || veo.Value == prevVal;
                        wi.Update();
                    }
                }
                while (!wi.ArgumentCalculator.AutoCalcVideoEncoderOptions.All(x => x.HasSettled));

                foreach (var sampleFile in sampleFiles)
                {
                    if (File.Exists(sampleFile)) File.Delete(sampleFile);
                }

                return;
            }
        }

        private static AutoPresetResult BalanceResults(IEnumerable<AutoPresetResult> validVals, decimal weightCompression = 1, decimal weightSpeed = 1, decimal weightSSIM = 1)
        {
            AutoPresetResult bestVal;
            var minCompression = validVals.Min(x => x.Compression);
            var devCompression = validVals.Max(x => x.Compression) - minCompression;

            var minSpeed = validVals.Min(x => x.Speed);
            var devSpeed = validVals.Max(x => x.Speed) - minSpeed;

            var minSSIM = validVals.Min(x => x.SSIM);
            var devSSIM = validVals.Max(x => x.SSIM) - minSSIM;

            bestVal = validVals.OrderByDescending(x =>
                (devCompression == 0 ? 0 : (1 - ((x.Compression - minCompression) / devCompression)) * weightCompression) +
                (devSpeed == 0 ? 0 : ((x.Speed - minSpeed) / devSpeed) * weightSpeed) +
                (devSSIM == 0 ? 0 : ((x.SSIM - minSSIM) / devSSIM) * weightSSIM)
                ).FirstOrDefault();
            return bestVal;
        }

        private static string GetArgument(ArgumentCalculator argCalc, int pass = 0)
        {
            var preset = argCalc.Preset;
            var firstPass = pass == 1;

            var audioArguments = string.Empty;


            if (!firstPass)
            {
                var audioStreamIndex = 0; //for stream output tracking

                foreach (var stream in argCalc.AudioStreams)
                {
                    foreach (var audioPreset in preset.AudioStreamPresets)
                    {
                        var match = audioPreset.Filters.All(f =>
                            f.Rule switch
                            {
                                AudioStreamRule.Any => true,
                                AudioStreamRule.Codec => f.Matches == f.Values.Contains(stream.codec_name.ToLower()),
                                AudioStreamRule.Channels => new List<int>() { stream.channels ?? 0 }.AsQueryable().Where($"it{f.NumberComparitor.Operator}{f.ChannelValue}").Any(),
                                AudioStreamRule.Language => stream.tags?.language == null || f.Matches == f.Values.Contains(stream.tags?.language.ToLower()),
                                _ => throw new NotImplementedException()
                            }
                        );

                        if (match)
                        {
                            var audioStreamMap = $" -map 0:{stream.index} -c:a:{audioStreamIndex++}";
                            audioArguments += audioPreset.Action switch
                            {
                                AudioStreamAction.Copy => $"{audioStreamMap} copy",
                                AudioStreamAction.Delete => "",
                                AudioStreamAction.DeleteUnlessOnly => preset.AudioStreamPresets.Last() == audioPreset && audioStreamIndex == 0 ? $"{audioStreamMap} copy" : "",
                                AudioStreamAction.Clone => $"{audioStreamMap} copy  -map 0:{stream.index} -c:a:{audioStreamIndex++} {audioPreset.Encoder.Name}{(string.IsNullOrWhiteSpace(audioPreset.BitRate) ? "" : $" -b:a:{audioStreamIndex} ")}{audioPreset.BitRate}",
                                AudioStreamAction.Encode => $"{audioStreamMap} {audioPreset.Encoder.Name}{(string.IsNullOrWhiteSpace(audioPreset.BitRate) ? "" : $" -b:a:{audioStreamIndex} ")}{audioPreset.BitRate}",
                                _ => throw new System.NotImplementedException()
                            };
                            break;
                        }
                    }
                }
            }


            var videoArguments = string.Empty;

            if (preset.VideoEncoder.IsCopy)
            {
                videoArguments = " -map 0:v -c:v copy";
            }
            else
            {

                var videoStreamIndex = 0; //for stream output tracking

                foreach (var vstream in argCalc.VideoStreams)
                {
                    var videoStreamMap = $" -map 0:{vstream.index} -c:v:{videoStreamIndex++}";

                    if (vstream.disposition.attached_pic)
                    {
                        videoArguments += $"{videoStreamMap} copy";
                    }
                    else
                    {

                        var videoOptionArgs = string.Join(" ",
                            argCalc.VideoEncoderOptions
                                .Where(x => (!string.IsNullOrWhiteSpace(x.Value) || (x.EncoderOption.IncludePass && argCalc.TwoPass)) && (!argCalc.TwoPass || !x.EncoderOption.DisabledByVideoBitRate))
                                .Select(x =>
                                    $"{x.EncoderOption.Arg.Replace("<val>", x.Value?.Trim())}{(x.EncoderOption.IncludePass && pass != 0 ? $" pass={pass}" : "")}"
                                )
                        );
                        videoArguments += $"{videoStreamMap} {preset.VideoEncoder.Name} {videoOptionArgs}";
                    }
                }

            }

            var hardwareDecoder = preset.HardwareDecoder.Wrap("-hwaccel {0} ");

            var opArgsStr = firstPass ?
               $" -an -f null {(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : @"/dev/null")}" :
               string.IsNullOrWhiteSpace(preset.OptionalArguments) ? "" : $" {preset.OptionalArguments.Trim()}";

            var bitrate = preset.VideoBitRate.HasValue ? $" -b:v {preset.VideoBitRate}k" : string.Empty;
            var frameRate = preset.FrameRate.HasValue ? $" -r {preset.FrameRate}" : string.Empty;
            var bframes = preset.B_Frames != 0 ? $" -bf {preset.B_Frames}" : string.Empty;
            var passStr = argCalc.TwoPass && !argCalc.VideoEncoderOptions.Any(vco => vco.EncoderOption.IncludePass) ? $" -pass {pass}" : string.Empty;

            var colorPrimaries = argCalc.ColorPrimaries.Wrap(" -color_primaries {0}");
            var colorTransfer = argCalc.ColorTransfer.Wrap(" -color_trc {0}");

            var globalVideoArgs = firstPass ? "" : $"{bitrate}{frameRate}{bframes}{colorPrimaries}{colorTransfer}{passStr}";

            var mapAllElse = firstPass ? "" : " -map 0:s? -c:s copy -map 0:t? -map 0:d? -movflags use_metadata_tags";

            var outputFile = firstPass ? "" : " \"{1}\"";

            return $"{hardwareDecoder}-y -i \"{{0}}\" {videoArguments}{opArgsStr}{globalVideoArgs}{audioArguments}{mapAllElse}{outputFile}";
        }
        

        private void StoreAutoCalcResults(int mediaID, int mediaInfoID, string argument, decimal ssim, long size, long originalSize, decimal speed, int sampleLength)
        {
            using (logger.BeginScope("StoreAutoCalcResults"))
            {
                using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));
                var results = db.GetCollection<AutoCalcResult>();
                var result = results.Query().Where(x => x.MediaInfoID == mediaInfoID && x.Argument == argument.Trim() && x.SampleLength == sampleLength).FirstOrDefault();

                if (result == default)
                {
                    result = new() { Argument = argument.Trim(), MediaInfoID = mediaInfoID, SampleLength = sampleLength };
                    try
                    {
                        results.Insert(result);
                    }
                    catch (LiteException)
                    {
                        db.DropCollection(typeof(AutoCalcResult).Name);
                        logger.LogInformation("Data insert error, dropping table");
                        results.Insert(result);
                    }
                }

                result.UniqueID = mediaID;
                result.Size = size;
                result.SSIM = ssim;
                result.OriginalSize = originalSize;
                result.Speed = speed;

                results.EnsureIndex(x => x.MediaInfoID);
                results.EnsureIndex(x => x.Argument);

                results.Update(result);
            }
        }
    }
}