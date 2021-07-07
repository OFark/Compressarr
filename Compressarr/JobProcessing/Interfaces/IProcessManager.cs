using Compressarr.FFmpeg.Events;
using Compressarr.FFmpeg.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using System;
using System.Collections.Generic;
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
        Task<EncodingResult> EncodeAVideo(DataReceivedEventHandler dataRecieved, ConversionProgressEventHandler dataProgress, string arguments, CancellationToken token);
        Task GenerateSamples(List<string> sampleFiles, WorkItem wi, CancellationToken token);
        Task<bool> Process(WorkItem workItem, CancellationToken token);
    }
}