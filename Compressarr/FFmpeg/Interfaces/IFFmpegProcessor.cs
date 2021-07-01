﻿using Compressarr.FFmpeg.Events;
using Compressarr.FFmpeg.Models;
using System;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public interface IFFmpegProcessor
    {
        Task<FFResult<string>> ConvertContainerToExtension(string container);
        Task<FFResult<CodecResponse>> GetAvailableCodecsAsync();
        Task<FFResult<ContainerResponse>> GetAvailableContainersAsync();
        Task<FFResult<EncoderResponse>> GetAvailableEncodersAsync();
        Task<FFResult<string>> GetAvailableHardwareDecodersAsync();
        Task<FFResult<string>> GetFFmpegVersionAsync();
        Task<FFResult<FFProbeResponse>> GetFFProbeInfo(string filePath);
        Task<FFResult<string>> GetFFProbeJSON(string filePath);
        Task<ProcessResponse> RunProcess(FFProcess process, string arguments, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null);
        Task<ProcessResponse> RunProcess(string filePath, string arguments, FFmpegProgressEvent OnProgress = null, FFmpegSSIMReportEvent OnSSIM = null);
    }
}