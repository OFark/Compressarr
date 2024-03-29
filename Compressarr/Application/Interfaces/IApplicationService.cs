﻿using Compressarr.Application.Models;
using Compressarr.FFmpeg.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public interface IApplicationService
    {
        event EventHandler<string> OnBroadcast;
        
        CancellationToken AppStoppingCancellationToken { get; set; }
        Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        SortedSet<string> DemuxerExtensions { get; set; }
        Dictionary<CodecType, SortedSet<Encoder>> Encoders { get; set; }
        string FFmpegVersion { get; set; }
        HashSet<Filter> Filters { get; set; }
        SortedSet<FFmpegFormat> Formats { get; set; }
        SortedSet<string> HardwareDecoders { get; set; }
        List<InitialisationTask> InitialisationSteps { get; set; }
        Task InitialiseFFmpeg { get; set; }
        Task InitialisePresets { get; set; }
        HashSet<Job> Jobs { get; set; }
        IEnumerable<Movie> Movies { get; set; }
        HashSet<FFmpegPreset> Presets { get; }
        double Progress { get; set; }
        APISettings RadarrSettings { get; set; }
        IEnumerable<Series> Series { get; set; }
        APISettings SonarrSettings { get; set; }
        void Broadcast(string message);
        LogLevel GetLogLevel();
        Task SaveAppSetting();
        Task UpdateLogLevel(LogLevel level);
    }
}