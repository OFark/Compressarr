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

namespace Compressarr.Application
{
    public interface IApplicationService
    {
        event EventHandler<string> OnBroadcast;

        Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        SortedDictionary<string, string> Containers { get; set; }
        Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        HashSet<Filter> Filters { get; set; }
        HashSet<Job> Jobs { get; set; }
        HashSet<FFmpegPreset> Presets { get; }
        APISettings RadarrSettings { get; set; }
        APISettings SonarrSettings { get; set; }
        string State { get; set; }
        AsyncManualResetEvent FFMpegReady { get; }
        AsyncManualResetEvent Initialised { get; }
        string FFmpegVersion { get; set; }
        bool LoadMediaInfoOnFilters { get; set; }
        bool InsertNamesIntoFFmpegPreviews { get; set; }
        IEnumerable<Movie> Movies { get; set; }
        bool AlwaysCalculateSSIM { get; set; }

        void Broadcast(string message);
        LogLevel GetLogLevel();
        Task SaveAppSetting();
        Task UpdateLogLevel(LogLevel level);
    }
}