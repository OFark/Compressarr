using Compressarr.FFmpeg.Events;
using Compressarr.FFmpeg.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public interface IFFmpegProcessor
    {
        Task<SSIMResult> CalculateSSIM(FFmpegProgressEvent ffmpegProgress, string sourceFile, string destinationFile, string hardwareDecoder, CancellationToken token);
        Task<FFResult<string>> ConvertContainerToExtension(string container, CancellationToken token);
        Task<FFResult> EncodeAVideo(string arguments, FFmpegProgressEvent fFmpegProgressEvent, FFmpegStdOutEvent fFmpegStdOutEvent, CancellationToken token);

        Task<FFResult<CodecResponse>> GetAvailableCodecsAsync(CancellationToken token);
        Task<FFResult<EncoderResponse>> GetAvailableEncodersAsync(CancellationToken token);

        Task<FFResult<FFmpegFormat>> GetAvailableFormatsAsync(CancellationToken token);
        Task<FFResult<string>> GetAvailableHardwareDecodersAsync(CancellationToken token);
        Task<FFResult<string>> GetFFmpegExtensionsAsync(IEnumerable<FFmpegFormat> formats, CancellationToken token);

        Task<FFResult<string>> GetFFmpegVersionAsync(CancellationToken token);
        Task<FFResult<FFProbeResponse>> GetFFProbeInfo(string filePath, CancellationToken token);
        Task<FFResult<string>> GetFFProbeJSON(string filePath, CancellationToken token);

        Task<IEnumerable<string>> GetValues(IEnumerable<FFmpegFormat> values, CancellationToken token);

        Task<ProcessResponse> RunProcess(FFProcess process, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null, FFmpegStdOutEvent OnStdOut = null);
        Task<ProcessResponse> RunProcess(string filePath, string arguments, CancellationToken token, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null, FFmpegStdOutEvent OnStdOut = null);
    }
}