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
        Regex ProgressReg { get; }
        Regex SSIMReg { get; }

        Task<SSIMResult> CalculateSSIM(IConversion converter, DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string sourceFile, string destinationFile, CancellationToken token);
        Task<EncodingResult> EncodeAVideo(IConversion converter, DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string arguments, CancellationToken token);
        Task Process(Job job);
    }
}