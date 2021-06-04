using Compressarr.FFmpeg.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace Compressarr.JobProcessing
{
    public interface IProcessManager
    {
        Task<SSIMResult> CalculateSSIM(DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string sourceFile, string destinationFile, string hardwareDecoder, CancellationToken token);
        Task<EncodingResult> EncodeAVideo(DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string arguments, CancellationToken token);
        Task Process(WorkItem workItem, CancellationToken token);
    }
}