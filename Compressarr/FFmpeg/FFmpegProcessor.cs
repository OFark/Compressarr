using Compressarr.Application;
using Compressarr.FFmpeg.Events;
using Compressarr.FFmpeg.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Compressarr.FFmpeg
{
    public partial class FFmpegProcessor : IFFmpegProcessor
    {
        private readonly static Regex RegexAvailableCodec = new(@"^\s([D\.])([E\.])([VAS])[I.][L\.][S\.]\s(?!=)([^\s]*).*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableEncoders = new(@"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableFormats = new(@"^ ?([D ])([E ]) (?!=)([^ ]*) *(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableHardwareAccels = new(@"^(?!Hardware).*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexMultipleExtensions = new(@"^ *Common extensions: ((\w+)[,\.])*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexSingleExtension = new(@"^ *Common extensions: (\w*)(?:,\w*)*\.", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexVersion = new(@"(?<=version\s)(.*)(?=\sCopy)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private static SemaphoreSlim semLock = new(1, 10);
        private readonly IApplicationService applicationService;
        private readonly IFileService fileService;
        private readonly ILogger<FFmpegProcessor> logger;

        public FFmpegProcessor(IApplicationService applicationService, IFileService fileService, ILogger<FFmpegProcessor> logger)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.logger = logger;
        }

        public async Task<SSIMResult> CalculateSSIM(FFmpegProgressEvent ffmpegProgress, string sourceFile, string destinationFile, string hardwareDecoder, CancellationToken token)
        {
            //var arguments = $"{hardwareDecoder} -i \"{destinationFile}\" -i \"{sourceFile}\" -lavfi  \"[0:v]settb=AVTB,setpts=PTS-STARTPTS[main];[1:v]settb=AVTB,setpts=PTS-STARTPTS[ref];[main][ref]ssim\" -max_muxing_queue_size 2048 -f null -";
            var arguments = $"{hardwareDecoder} -i \"{destinationFile}\" -i \"{sourceFile}\" -lavfi  \"[0:v]settb=AVTB,setpts=N[main];[1:v]settb=AVTB,setpts=N[ref];[main][ref]ssim\" -max_muxing_queue_size 2048 -f null -";
            logger.LogDebug($"SSIM arguments: {arguments}");

            decimal ssim = default;

            try
            {
                logger.LogDebug("FFmpeg starting SSIM.");
                var result = await RunProcess(FFProcess.FFmpeg, arguments, token, ffmpegProgress, (ssimresult) => ssim = ssimresult.SSIM);
                logger.LogDebug($"FFmpeg finished SSIM. ({ssim})");

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

        public async Task<FFResult> EncodeAVideo(string arguments, FFmpegProgressEvent fFmpegProgressEvent, FFmpegStdOutEvent fFmpegStdOutEvent, CancellationToken token)
        {
            using (logger.BeginScope("Converting video"))
            {
                try
                {
                    logger.LogDebug("FFmpeg conversion started.");
                    var result = await RunProcess(FFProcess.FFmpeg, arguments, token, fFmpegProgressEvent, null, fFmpegStdOutEvent);
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
        }

        public async Task<FFResult<string>> ConvertContainerToExtension(string container, CancellationToken token)
        {
            await applicationService.InitialiseFFmpeg;

            using (logger.BeginScope("Converting container to extension"))
            {
                logger.LogInformation($"Container name: {container}");

                if (container == "copy")
                {
                    return new(false, (string)null);
                }

                var result = await RunProcess(FFProcess.FFmpeg, $"-v 1 -h muxer={container}", token);

                if (result.Success)
                {
                    var match = RegexSingleExtension.Match(result.StdOut);
                    if (match != null)
                    {
                        return new(true, match.Groups[1].Value);
                    }
                }

                return new(false, container);
            }
        }

        public async Task<FFResult<CodecResponse>> GetAvailableCodecsAsync(CancellationToken token)
        {
            using (logger.BeginScope("Get Available Codecs"))
            {
                try
                {

                    var result = await RunProcess(FFProcess.FFmpeg, "-codecs -v 1", token);

                    if (result.Success)
                    {
                        var results = new HashSet<CodecResponse>();

                        return new(true, RegexAvailableCodec.Matches(result.StdOut).Select(m => new CodecResponse()
                        {
                            IsDecoder = m.Groups[1].Value == "D",
                            IsEncoder = m.Groups[2].Value == "E",
                            Type = m.Groups[3].Value switch { "A" => CodecType.Audio, "S" => CodecType.Subtitle, "V" => CodecType.Video, _ => throw new NotImplementedException() },
                            Name = m.Groups[4].Value,
                            Description = m.Groups[5].Value
                        }));
                    }

                    return new(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred");
                    return new(ex);
                }
            }
        }

        public async Task<FFResult<EncoderResponse>> GetAvailableEncodersAsync(CancellationToken token)
        {
            using (logger.BeginScope("Get Available Encoders"))
            {

                var result = await RunProcess(FFProcess.FFmpeg, "-encoders -v 1", token);

                if (result.Success)
                {
                    var results = new HashSet<EncoderResponse>();
                    foreach (Match m in RegexAvailableEncoders.Matches(result.StdOut))
                    {
                        results.Add(new()
                        {
                            Type = m.Groups[1].Value switch { "A" => CodecType.Audio, "S" => CodecType.Subtitle, "V" => CodecType.Video, _ => throw new NotImplementedException() },
                            Name = m.Groups[2].Value,
                            Description = m.Groups[3].Value
                        });
                    }

                    return new(true, results);
                }

                return new(result);
            }
        }

        public async Task<FFResult<FFmpegFormat>> GetAvailableFormatsAsync(CancellationToken token)
        {
            using (logger.BeginScope($"Get Available Formats"))
            {
                try
                {
                    var result = await RunProcess(FFProcess.FFmpeg, "-formats -v 1", token);

                    if (result.Success)
                    {
                        return new(true, RegexAvailableFormats.Matches(result.StdOut).Select(m => new FFmpegFormat()
                        {
                            Demuxer = m.Groups[1].Value == "D",
                            Muxer = m.Groups[2].Value == "E",
                            Name = m.Groups[3].Value,
                            Description = m.Groups[4].Value
                        }));
                    }

                    return new(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred");
                    return new(ex);
                }
            }
        }

        public async Task<FFResult<string>> GetAvailableHardwareDecodersAsync(CancellationToken token)
        {
            using (logger.BeginScope("Get available hardware decoders"))
            {
                var hwdecoders = new SortedSet<string>();

                var result = await RunProcess(FFProcess.FFmpeg, "-hwaccels -v 1", token);

                if (result.Success)
                {
                    foreach (Match m in RegexAvailableHardwareAccels.Matches(result.StdOut))
                    {
                        if (!string.IsNullOrWhiteSpace(m.Value))
                        {
                            hwdecoders.Add(m.Value.Trim());
                        }
                    }
                    return new(true, hwdecoders);
                }

                return new(result);
            }
        }

        public async Task<FFResult<string>> GetFFmpegExtensionsAsync(IEnumerable<FFmpegFormat> formats, CancellationToken token)
        {
            using (logger.BeginScope($"Get FFmpeg Extensions"))
            {
                try
                {
                    var results = new ConcurrentBag<string>();
                    var extensions = new ConcurrentBag<string>();

                    semLock = new(10, 10);


                    await formats.Where(f => f.Demuxer).AsyncParallelForEach(async format =>
                    {
                        var result = await RunProcess(fileService.FFMPEGPath, $"-v 1 -h demuxer={format.Name}", token);
                        if (result.Success)
                        {
                            var match = RegexMultipleExtensions.Match(result.StdOut);
                            if (match != null & match.Success)
                            {
                                foreach (var ext in match.Groups[2].Captures.Select(c => c.Value))
                                {
                                    extensions.Add(ext);
                                }
                            }
                            else
                            {
                                logger.LogInformation($"No Match: {result}");
                            }
                        }
                    }).ConfigureAwait(false);

                    semLock = new(1, 1);

                    if (extensions.Any())
                    {
                        logger.LogInformation($"FFmpeg Demuxer extensions: {extensions.Count}");
                        return new(true, extensions);
                    }

                    return new(false, (string)null);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred");
                    return new(ex);
                }
            }
        }

        public async Task<IEnumerable<string>> GetValues(IEnumerable<FFmpegFormat> formats, CancellationToken token)
        {
            using (logger.BeginScope($"Get FFmpeg Extensions"))
            {
                try
                {
                    var results = new ConcurrentBag<string>();
                    var extensions = new List<string>();

                    semLock = new(10, 10);


                    await formats.Where(f => f.Demuxer).AsyncParallelForEach(async format =>
                    {
                        await Task.Delay(1000);
                        extensions.Add(format.Name);
                    }).ConfigureAwait(false);

                    logger.LogInformation($"Output count: {extensions.Count}");

                    return extensions;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred");
                    return null;
                }
            }
        }

        public Task AsyncParallelForEach<T>(IEnumerable<T> source, Func<T, Task> body, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded, TaskScheduler scheduler = null)
        {
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            if (scheduler != null)
                options.TaskScheduler = scheduler;

            var block = new ActionBlock<T>(body, options);

            foreach (var item in source)
                block.Post(item);

            block.Complete();
            return block.Completion;
        }




        public async Task<FFResult<string>> GetFFmpegVersionAsync(CancellationToken token)
        {
            using (logger.BeginScope("Get FFmpeg Version."))
            {
                try
                {
                    if (fileService.HasFile(AppFile.ffmpegVersion))
                    {
                        var version = await fileService.ReadJsonFileAsync<dynamic>(AppFile.ffmpegVersion);

                        if (version != null)
                        {
                            return new(true, version.version?.ToString());
                        }
                        else
                        {
                            logger.LogDebug($"Empty file.");
                        }
                    }


                    logger.LogDebug($"Version file missing. Trying manually");

                    var result = await RunProcess(FFProcess.FFmpeg, "-version", token);

                    if (result.Success)
                    {
                        var match = RegexVersion.Match(result.StdOut);
                        if (match != null)
                        {
                            return new(true, match.Value);
                        }
                    }

                    return new(result);
                }
                catch (UnauthorizedAccessException uae)
                {
                    logger.LogError(uae, "An error occurred");
                    return new(uae);
                }
            }
        }
        public async Task<FFResult<FFProbeResponse>> GetFFProbeInfo(string filePath, CancellationToken token)
        {
            using (logger.BeginScope("GetFFProbeInfo: {filePath}", filePath))
            {
                try
                {
                    var result = await RunProcess(FFProcess.FFprobe, $"-v 1 -print_format json -show_format -show_streams -find_stream_info \"{filePath}\"", token);

                    if (result.Success)
                    {
                        var ffProbeInfo = JsonConvert.DeserializeObject<FFProbeResponse>(result.StdOut);
                        return new(true, ffProbeInfo);
                    }

                    return new(result);
                }
                catch (ArgumentException aex)
                {
                    logger.LogError(aex, "An error occurred");
                    return new(aex);
                }
            }
        }

        public async Task<FFResult<string>> GetFFProbeJSON(string filePath, CancellationToken token)
        {
            using (logger.BeginScope("GetFFProbeInfo: {filePath}", filePath))
            {
                try
                {
                    var result = await RunProcess(FFProcess.FFprobe, $"-v 1 -print_format json -show_format -show_streams -find_stream_info \"{filePath}\"", token);

                    if (result.Success)
                    {

                        return new(true, result.StdOut);
                    }

                    return new(result);
                }
                catch (ArgumentException aex)
                {
                    logger.LogError(aex, "An error occurred");
                    return new(aex);
                }
            }
        }
        public async Task<ProcessResponse> RunProcess(FFProcess process, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null, FFmpegStdOutEvent OnStdOut = null)
        {
            var filePath = process switch
            {
                FFProcess.FFmpeg => fileService.FFMPEGPath,
                FFProcess.FFprobe => fileService.FFPROBEPath,
                _ => throw new NotImplementedException()
            };

            return await RunProcess(filePath, arguments, token, OnProgress, OnSSIM, OnStdOut);
        }

        public async Task<ProcessResponse> RunProcess(string filePath, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null, FFmpegStdOutEvent OnStdOut = null)
        {
            using (logger.BeginScope("Run Process: {filePath}", filePath))
            {
                var response = new ProcessResponse();


                using var process = new Process();
                process.StartInfo = new ProcessStartInfo()
                {
                    Arguments = arguments,
                    CreateNoWindow = true,
                    FileName = filePath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                var stdOut = string.Empty;
                var stdErr = string.Empty;
                TimeSpan duration = default;
                FFmpegProgress progressReport = null;

                await semLock.WaitAsync(token);
                try
                {
                    var outputCloseEvent = new TaskCompletionSource<bool>();

                    process.OutputDataReceived += (s, e) =>
                    {
                        // The output stream has been closed i.e. the process has terminated
                        if (e.Data == null)
                        {
                            if(!outputCloseEvent.Task.IsCompleted)
                            {
                                outputCloseEvent.SetResult(true);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {

                                if (OnProgress != null && FFmpegProgress.TryParse(e.Data, out var result))
                                {
                                    progressReport = result.CalculatePercentage(duration);
                                    OnProgress.Invoke(progressReport);
                                }
                                else if (OnSSIM != null && FFmpegSSIMReport.TryParse(e.Data, out var ssimreport)) OnSSIM.Invoke(ssimreport);
                                else if (FFmpegDurationReport.TryParse(e.Data, out var durationreport)) duration = duration != default && duration > durationreport.Duration ? duration : durationreport.Duration;
                                else
                                {
                                    OnStdOut?.Invoke(e.Data);
                                    stdOut += $"{e.Data}\r\n";
                                }
                            }
                        }
                    };

                    var errorCloseEvent = new TaskCompletionSource<bool>();

                    process.ErrorDataReceived += (s, e) =>
                    {
                        // The error stream has been closed i.e. the process has terminated
                        if (e.Data == null)
                        {
                            if (!errorCloseEvent.Task.IsCompleted)
                            {
                                errorCloseEvent.SetResult(true);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                if (OnProgress != null && FFmpegProgress.TryParse(e.Data, out var result))
                                {
                                    progressReport = result.CalculatePercentage(duration);
                                    OnProgress.Invoke(progressReport);
                                }
                                else if (OnSSIM != null && FFmpegSSIMReport.TryParse(e.Data, out var ssimreport)) OnSSIM.Invoke(ssimreport);
                                else if (FFmpegDurationReport.TryParse(e.Data, out var durationreport)) duration = duration != default && duration > durationreport.Duration ? duration : durationreport.Duration;
                                else
                                {
                                    OnStdOut?.Invoke(e.Data);
                                    logger.LogWarning(e.Data);
                                    stdErr += $"{e.Data}\r\n";
                                }
                            }
                        }
                    };

                    using (token.Register(() =>
                    {
                        outputCloseEvent.TrySetCanceled(token);
                        errorCloseEvent.TrySetCanceled(token);
                        process.Kill();
                    }))
                    {

                        bool isStarted;

                        try
                        {
                            logger.LogDebug($"Starting process: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
                            isStarted = process.Start();
                        }
                        catch (Exception error)
                        {
                            // Usually it occurs when an executable file is not found or is not executable

                            response.Success = false;
                            response.ExitCode = -1;
                            response.StdErr = error.Message;
                            response.StdOut = error.Message;

                            isStarted = false;
                        }

                        if (isStarted)
                        {
                            // Reads the output stream first and then waits because deadlocks are possible
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();


                            // Creates task to wait for process exit using timeout
                            var waitForExit = process.WaitForExitAsync(token);


                            // Create task to wait for process exit and closing all output streams
                            var processTask = Task.WhenAll(waitForExit, outputCloseEvent.Task, errorCloseEvent.Task);

                            await processTask;


                            if (!token.IsCancellationRequested)
                            {
                                response.StdOut = stdOut;
                                response.StdErr = stdErr;

                                if (progressReport != null)
                                {
                                    progressReport.Percentage = 100;
                                    OnProgress.Invoke(progressReport);
                                }

                                response.ExitCode = process.ExitCode;

                                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(response.StdErr))
                                {
                                    logger.LogError($"Process Error: ({process.ExitCode}) {response.StdErr} <End Of Error>");
                                }
                                else
                                {
                                    response.Success = true;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    semLock.Release();
                }
                return response;
            }
        }
    }
}
