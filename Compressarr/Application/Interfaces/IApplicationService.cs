using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public interface IApplicationService
    {
        event EventHandler<string> OnBroadcast;

        bool AlwaysCalculateSSIM { get; set; }
        Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        SortedDictionary<string, string> Containers { get; set; }
        Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        string FFmpegVersion { get; set; }
        HashSet<Filter> Filters { get; set; }
        SortedSet<string> HardwareDecoders { get; set; }
        Task InitialiseFFmpeg { get; set; }
        Task InitialisePresets { get; set; }
        bool InsertNamesIntoFFmpegPreviews { get; set; }
        HashSet<Job> Jobs { get; set; }
        IEnumerable<Movie> Movies { get; set; }
        HashSet<FFmpegPreset> Presets { get; }
        double Progress { get; set; }
        APISettings RadarrSettings { get; set; }
        APISettings SonarrSettings { get; set; }
        string State { get; set; }
        Queue<string> StateHistory { get; set; }
        bool CacheMediaInfo { get; set; }
        CancellationToken AppStoppingCancellationToken { get; set; }

        void Broadcast(string message);
        LogLevel GetLogLevel();
        Task SaveAppSetting();
        Task UpdateLogLevel(LogLevel level);
    }
}