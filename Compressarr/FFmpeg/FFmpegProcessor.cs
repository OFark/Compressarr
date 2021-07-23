using Compressarr.Application;
using Compressarr.FFmpeg.Events;
using Compressarr.FFmpeg.Models;
using Compressarr.Presets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public partial class FFmpegProcessor : IFFmpegProcessor
    {
        private readonly static Regex RegexAvailableCodec = new(@"^\s([D\.])([E\.])([VAS])[I.][L\.][S\.]\s(?!=)([^\s]*).*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableEncoders = new(@"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableFormats = new(@"^ ?([D ])([E ]) (?!=)([^ ]*) *(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexAvailableHardwareAccels = new(@"^(?!Hardware).*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexMultipleExtensions = new Regex(@"^ *Common extensions: ((\w+)[,\.])*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexSingleExtension = new(@"^ *Common extensions: (\w*)(?:,\w*)*\.", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private readonly static Regex RegexVersion = new Regex(@"(?<=version\s)(.*)(?=\sCopy)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private static SemaphoreSlim semLock = new(1, 1);
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
                    var results = new List<string>();
                    foreach (var format in formats.Where(x => x.Demuxer))
                    {
                        var result = await RunProcess(FFProcess.FFmpeg, $"-v 1 -h demuxer={format.Name}", token);

                        if (result.Success)
                        {
                            var match = RegexMultipleExtensions.Match(result.StdOut);
                            if (match != null)
                            {
                                results.AddRange(match.Groups[2].Captures.Select(c => c.Value).ToList());
                            }
                        }
                    }

                    if (results.Any())
                    {
                        return new(true, results.Distinct());
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
        public async Task<ProcessResponse> RunProcess(FFProcess process, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null)
        {
            var filePath = process switch
            {
                FFProcess.FFmpeg => fileService.FFMPEGPath,
                FFProcess.FFprobe => fileService.FFPROBEPath,
                _ => throw new NotImplementedException()
            };

            return await RunProcess(filePath, arguments, token, OnProgress, OnSSIM);
        }

        public async Task<ProcessResponse> RunProcess(string filePath, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null)
        {
            using (logger.BeginScope("Run Process: {filePath}", filePath))
            {
                var response = new ProcessResponse();

                using var p = new Process();
                p.StartInfo = new ProcessStartInfo()
                {
                    Arguments = arguments,
                    CreateNoWindow = true,
                    FileName = filePath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                var stdOut = new StringBuilder();
                var stdErr = new StringBuilder();
                TimeSpan duration = default;
                FFmpegProgress progressReport = null;

                p.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e?.Data))
                    {
                        if (OnProgress != null && FFmpegProgress.TryParse(e.Data, out var result))
                        {
                            progressReport = result.CalculatePercentage(duration);
                            OnProgress.Invoke(progressReport);
                        }
                        else if (OnSSIM != null && FFmpegSSIMReport.TryParse(e.Data, out var ssimreport)) OnSSIM.Invoke(ssimreport);
                        else if (FFmpegDurationReport.TryParse(e.Data, out var durationreport)) duration = duration != default && duration > durationreport.Duration ? duration : durationreport.Duration;
                        else { stdOut.AppendLine(e.Data); }
                    }
                });
                p.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e?.Data))
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
                            logger.LogError(e.Data);
                            stdErr.AppendLine(e.Data);
                        }
                    }
                });

                await semLock.WaitAsync(token);
                try
                {
                    logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                    p.Start();

                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    await p.WaitForExitAsync(token);
                    p.WaitForExit(1000); //This waits for any handles to finish as well. 
                }
                finally
                {
                    semLock.Release();
                }


                response.StdOut = stdOut.ToString();
                response.StdErr = stdErr.ToString();

                if (progressReport != null)
                {
                    progressReport.Percentage = 100;
                    OnProgress.Invoke(progressReport);
                }

                response.ExitCode = p.ExitCode;

                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(response.StdErr))
                {
                    logger.LogError($"Process Error: ({p.ExitCode}) {response.StdErr} <End Of Error>");
                }
                else
                {
                    response.Success = true;
                }

                return response;
            }
        }
    }
}
