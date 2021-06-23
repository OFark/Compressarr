using Compressarr.Application;
using Compressarr.FFmpeg;
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

namespace Compressarr.JobProcessing
{
    public class ArgumentService : IArgumentService
    {
        private readonly IApplicationService applicationService;
        private readonly IFileService fileService;
        private readonly ILogger<ArgumentService> logger;
        private readonly IMediaInfoService mediaInfoService;
        private readonly IProcessManager processManager;
        public ArgumentService(IApplicationService applicationService, IFileService fileService, ILogger<ArgumentService> logger, IMediaInfoService mediaInfoService, IProcessManager processManager)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
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

        public async Task SetArguments(FFmpegPreset preset, WorkItem wi, CancellationToken token)
        {

            using (logger.BeginScope("Get Arguments"))
            {
                await applicationService.InitialisePresets;

                wi.Media.MediaInfo ??= await mediaInfoService.GetMediaInfo(wi.Media);

                wi.ArgumentCalculator = new ArgumentCalculator(wi, preset);

                await CalculateBestOptions(wi, token);

                if (token.IsCancellationRequested) return;

                wi.Arguments = GetArguments(wi);
            }
        }

        private async Task CalculateBestOptions(WorkItem wi, CancellationToken token)
        {
            using (logger.BeginScope("Calculating Best Encoder Options"))
            {
                var preset = wi.ArgumentCalculator.Preset;

                var sampleFile = Path.Combine(fileService.TempDir, $"directCopy.{preset.ContainerExtension}");
                var tempEncFile = Path.Combine(fileService.TempDir, $"encFile.{preset.ContainerExtension}");

                Task genSampleTask = null;

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

                        if(!veo.HasSettled) veo.ArgumentHistory.Add(currArg);
                        // If it is in the history then the next steps should auto fill from cache

                        foreach (var best in veo.AutoPresetTests.Where(x => x.Best)) { best.Best = false; }

                        foreach (var test in veo.AutoPresetTests)
                        {
                            if (token.IsCancellationRequested) return;

                            veo.Value = test.ArgumentValue;

                            test.Reset();
                            test.Processing = true;

                            wi.Update();

                            //var autoTuneStr = $" {test.Argument.Replace("<val>", i.Key)}";
                            //var arg = $"{hardwareDecoder}-y -i \"{sampleFile}\" -map 0:V -c:V {preset.VideoEncoder.Name}{autoTuneStr} {frameRate} {optionalArguments}  \"{tempEncFile}\" ";

                            var arg = string.Format(GetArgument(wi.ArgumentCalculator), sampleFile, tempEncFile);

                            logger.LogInformation($"Trying: {arg}");

                            var storedResult = GetAutoCalcResult(wi.Media.MediaInfo.GetStableHash(), arg);

                            if (storedResult != null)
                            {
                                test.AddSize(storedResult.Size, storedResult.OriginalSize);
                                test.SSIM = storedResult.SSIM;
                                logger.LogDebug($"SSIM: {storedResult.SSIM}, Size: {storedResult.Size.ToFileSize()} {Math.Round((decimal)storedResult.Size / storedResult.OriginalSize * 100M, 2).Adorn("%")}");
                            }
                            else
                            {
                                genSampleTask ??= processManager.GenerateSample(sampleFile, wi, preset, token);
                                await genSampleTask;

                                var origSize = new FileInfo(sampleFile).Length;

                                logger.LogInformation($"Testing sample encode with: {arg}");

                                await processManager.EncodeAVideo(null, (sender, args) =>
                                {
                                    test.EncodingProgress = args.Percent;
                                    wi.Update();
                                }, arg, token);

                                if (token.IsCancellationRequested) return;

                                var size = new FileInfo(tempEncFile).Length;
                                test.AddSize(size, origSize);
                                wi.Update();

                                var ssimResult = await processManager.CalculateSSIM(null, (sender, args) =>
                                {
                                    test.SSIMProgress = args.Percent;
                                    wi.Update();
                                }, sampleFile, tempEncFile, hardwareDecoder, token);

                                if (ssimResult.Success)
                                {
                                    test.SSIM = ssimResult.SSIM;
                                    logger.LogDebug($"SSIM: {ssimResult.SSIM}, Size: {size.ToFileSize()} {Math.Round((decimal)size / origSize * 100M, 2).Adorn("%")}");
                                    StoreAutoCalcResults(wi.Media.UniqueID, wi.Media.MediaInfo.GetStableHash(), arg, ssimResult.SSIM, size, origSize);
                                }
                            }

                            test.Processing = false;
                            wi.Update();
                        }

                        AutoPresetResult bestVal = default;

                        if (applicationService.AutoCalculationType == AutoCalcType.BangForBuck)
                        {
                            var validVals = veo.AutoPresetTests.Where(x => x != null && x.Size < x.OriginalSize);

                            var minSSIM = validVals.Min(x => x.SSIM);
                            var devSSIM = validVals.Max(x => x.SSIM) - minSSIM;

                            var minSize = validVals.Min(x => x.Size);
                            var devSize = validVals.Max(x => x.Size) - minSize;

                            // clear out any outliers, sometimes FFmpeg goes way off kilter with one value that can skew the result set.
                            while (devSSIM != 0 && devSize != 0 && validVals.Select(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Size - minSize) / (decimal)devSize)).Any(y => y == -1) && validVals.Count() > 2)
                            {
                                validVals = validVals.Where(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Size - minSize) / (decimal)devSize) > -1);
                                minSSIM = validVals.Min(x => x.SSIM);
                                devSSIM = validVals.Max(x => x.SSIM) - minSSIM;

                                minSize = validVals.Min(x => x.Size);
                                devSize = validVals.Max(x => x.Size) - minSize;
                            }

                            if (devSSIM == 0)
                            {
                                bestVal = validVals.OrderBy(x => x.Size).FirstOrDefault();
                            }
                            else if (devSize == 0)
                            {
                                bestVal = validVals.OrderByDescending(x => x.SSIM).FirstOrDefault();
                            }
                            else
                            {
                                bestVal = validVals.OrderByDescending(x => ((x.SSIM - minSSIM) / devSSIM) - ((x.Size - minSize) / (decimal)devSize)).FirstOrDefault();
                            }
                        }

                        if (applicationService.AutoCalculationType == AutoCalcType.FirstPastThePost)
                        {
                            bestVal = veo.AutoPresetTests.Where(x => x != null && x.SSIM >= (applicationService.AutoCalculationPost ?? 99)).OrderBy(x => x.Size).ThenByDescending(x => x.SSIM).FirstOrDefault();
                        }

                        if (bestVal.Equals(default(KeyValuePair<string, AutoPresetResult>)))
                        {
                            bestVal = veo.AutoPresetTests.Where(x => x != null && x.Size < x.OriginalSize).OrderByDescending(x => x.SSIM).ThenBy(x => x.Size).FirstOrDefault();
                        };

                        if (bestVal.Equals(default(KeyValuePair<string, AutoPresetResult>)))
                        {
                            bestVal = veo.AutoPresetTests.OrderBy(x => x.Size).FirstOrDefault();
                        }

                        bestVal.Best = true;

                        var resultLog = veo.AutoPresetTests.Select(d => $"{d.ArgumentValue} | {d.SSIM} | {d.Percent.Adorn("%")} {(d.Best ? "<=" : "")}");
                        logger.LogDebug($"SSIM results:\r\n{string.Join("\r\n", resultLog)}");


                        wi.Output($"Best Argument: {veo.EncoderOption.Arg.Replace("<val>", bestVal.ArgumentValue)}");
                        veo.Value = bestVal.ArgumentValue;
                        veo.HasSettled = veo.HasSettled || veo.Value == prevVal;
                        wi.Update();
                    }
                }
                while (!wi.ArgumentCalculator.AutoCalcVideoEncoderOptions.All(x => x.HasSettled));

                if (File.Exists(sampleFile)) File.Delete(sampleFile);
                if (File.Exists(tempEncFile)) File.Delete(tempEncFile);

                return;
            }
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
                                .Where(x => !string.IsNullOrWhiteSpace(x.Value) && (!argCalc.TwoPass || !x.EncoderOption.DisabledByVideoBitRate))
                                .Select(x =>
                                    $"{x.EncoderOption.Arg.Replace("<val>", x.Value.Trim())}{(x.EncoderOption.IncludePass && pass != 0 ? $" pass={pass}" : "")}"
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

            var globalVideoArgs = $"{bitrate}{frameRate}{bframes}{colorPrimaries}{colorTransfer}{passStr}";

            var mapAllElse = firstPass ? "" : " -map 0:s? -c:s copy -map 0:t? -map 0:d? -movflags use_metadata_tags";

            return $"{hardwareDecoder}-y -i \"{{0}}\" {videoArguments}{opArgsStr}{globalVideoArgs}{audioArguments}{mapAllElse} \"{{1}}\"";
        }
        private AutoCalcResult GetAutoCalcResult(int mediaInfoID, string argument)
        {
            using (logger.BeginScope("StoreAutoCalcResults"))
            {
                var sampleLength = applicationService.ArgCalcSampleSeconds.ToString();

                using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));
                var results = db.GetCollection<AutoCalcResult>();
                var result = results.Query().Where(x => x.MediaInfoID == mediaInfoID && x.Argument == argument.Trim() && x.SampleLength == sampleLength).FirstOrDefault();

                return result;
            }
        }

        private void StoreAutoCalcResults(int mediaID, int mediaInfoID, string argument, decimal ssim, long size, long originalSize)
        {
            using (logger.BeginScope("StoreAutoCalcResults"))
            {
                var sampleLength = applicationService.ArgCalcSampleSeconds.ToString();

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

                results.EnsureIndex(x => x.MediaInfoID);
                results.EnsureIndex(x => x.Argument);

                results.Update(result);
            }
        }
    }
}
