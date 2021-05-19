using Compressarr.Application.Models;
using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Compressarr.Application
{
    public interface IApplicationService
    {
        event EventHandler<string> OnBroadcast;

        bool AlwaysCalculateSSIM { get; set; }
        Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        SortedDictionary<string, string> Containers { get; set; }
        Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        AsyncManualResetEvent FFMpegReady { get; }
        string FFmpegVersion { get; set; }
        HashSet<Filter> Filters { get; set; }
        SortedSet<string> HardwareDecoders { get; set; }
        AsyncManualResetEvent Initialised { get; }
        bool InsertNamesIntoFFmpegPreviews { get; set; }
        HashSet<Job> Jobs { get; set; }
        bool LoadMediaInfoOnFilters { get; set; }
        IEnumerable<Movie> Movies { get; set; }
        HashSet<FFmpegPreset> Presets { get; }
        double Progress { get; set; }
        APISettings RadarrSettings { get; set; }
        APISettings SonarrSettings { get; set; }
        string State { get; set; }
        Queue<string> StateHistory { get; set; }
        bool CacheMediaInfo { get; set; }

        void Broadcast(string message);
        LogLevel GetLogLevel();
        Task SaveAppSetting();
        Task UpdateLogLevel(LogLevel level);
        Task<ProcessResponse> RunProcess(string filePath, string arguments);
    }
}