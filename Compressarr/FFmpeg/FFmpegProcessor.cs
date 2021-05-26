using Compressarr.Application;
using Compressarr.FFmpeg.Models;
using Compressarr.Presets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public class FFmpegProcessor : IFFmpegProcessor
    {
        private readonly IApplicationService applicationService;
        private readonly IFileService fileService;
        private readonly ILogger<FFmpegProcessor> logger;
        public FFmpegProcessor(IApplicationService applicationService, IFileService fileService, ILogger<FFmpegProcessor> logger)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.logger = logger;
        }

        public async Task<FFResult<EncoderResponse>> GetAvailableEncodersAsync()
        {
            using (logger.BeginScope("Get Available Encoders"))
            {

                var result = await RunProcess(FFProcess.FFmpeg, "-encoders -v 1");

                if (result.Success)
                {
                    var regPattern = @"^\s([VAS])[F\.][S\.][X\.][B\.][D\.]\s(?!=)([^\s]*)\s*(.*)$";
                    var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                    var results = new HashSet<EncoderResponse>();
                    foreach (Match m in reg.Matches(result.StdOut))
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

        public async Task<FFResult<string>> GetAvailableHardwareDecodersAsync()
        {
            using (logger.BeginScope("Get available hardware decoders"))
            {
                var hwdecoders = new SortedSet<string>();

                var result = await RunProcess(FFProcess.FFmpeg, "-hwaccels -v 1");

                if (result.Success)
                {
                    var regPattern = @"^(?!Hardware).*";
                    var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                    foreach (Match m in reg.Matches(result.StdOut))
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

        public async Task<FFResult<CodecResponse>> GetAvailableCodecsAsync()
        {
            using (logger.BeginScope("Get Available Codecs"))
            {
                try
                {

                    var result = await RunProcess(FFProcess.FFmpeg, "-codecs -v 1");

                    if (result.Success)
                    {
                        var regPattern = @"^\s([D\.])([E\.])([VAS])[I.][L\.][S\.]\s(?!=)([^\s]*).*$";
                        var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                        var results = new HashSet<CodecResponse>();

                        return new(true, reg.Matches(result.StdOut).Select(m => new CodecResponse()
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

        public async Task<FFResult<ContainerResponse>> GetAvailableContainersAsync()
        {
            using (logger.BeginScope($"Get Available Containers"))
            {
                try
                {
                    var result = await RunProcess(FFProcess.FFmpeg, "-formats -v 1");

                    if (result.Success)
                    {
                        var regPattern = @"^ ?([D ])([E ]) (?!=)([^ ]*) *(.*)$";
                        var reg = new Regex(regPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        logger.LogDebug($"Regex matching pattern: \"{regPattern}\"");

                        return new(true, reg.Matches(result.StdOut).Where(m => m.Groups[2].Value == "E").Select(m => new ContainerResponse()
                        {
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

        public async Task<FFResult<string>> GetFFmpegVersionAsync()
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

                    var result = await RunProcess(FFProcess.FFmpeg, "-version");

                    if (result.Success)
                    {
                        var reg = new Regex(@"(?<=version\s)(.*)(?=\sCopy)");
                        var match = reg.Match(result.StdOut);
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

        public async Task<FFResult<string>> ConvertContainerToExtension(string container)
        {
            await applicationService.InitialiseFFmpeg;

            using (logger.BeginScope("Converting container to extension"))
            {
                logger.LogInformation($"Container name: {container}");

                if (container == "copy")
                {
                    return new(false, (string)null);
                }

                var result = await RunProcess(FFProcess.FFmpeg, $"-v 1 -h muxer={container}");

                if (result.Success)
                {
                    var reg = new Regex(@"^ *Common extensions: (\w*)(?:,\w*)*\.", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    var match = reg.Match(result.StdOut);
                    if (match != null)
                    {
                        return new(true, match.Groups[1].Value);
                    }
                }

                return new(false, container);
            }
        }

        public async Task<FFResult<FFProbeResponse>> GetFFProbeInfo(string filePath)
        {
            using (logger.BeginScope("GetFFProbeInfo: {filePath}", filePath))
            {
                try
                {
                    var result = await RunProcess(FFProcess.FFprobe, $"-v 1 -print_format json -show_format -show_streams -find_stream_info \"{filePath}\"");

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

        public async Task<ProcessResponse> RunProcess(FFProcess process, string arguments)
        {   
            var filePath = process switch
            {
                FFProcess.FFmpeg => fileService.FFMPEGPath,
                FFProcess.FFprobe => fileService.FFPROBEPath,
                _ => throw new NotImplementedException()
            };

            return await RunProcess(filePath, arguments);
        }

        public async Task<ProcessResponse> RunProcess(string filePath, string arguments)
        {
            using (logger.BeginScope("Run Process: {filePath}", filePath))
            {
                var response = new ProcessResponse();

                using (var p = new Process())
                {
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

                    logger.LogDebug($"Starting process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                    p.Start();
                    response.StdOut = p.StandardOutput.ReadToEnd();
                    response.StdErr = p.StandardError.ReadToEnd();
                    await p.WaitForExitAsync();

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
}
