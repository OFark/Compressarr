using Compressarr.FFmpeg.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Compressarr.Services.Base;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Presets
{
    public interface IPresetManager : IJobDependency
    {
        SortedSet<Codec> AudioCodecs { get; }
        SortedSet<Encoder> AudioEncoders { get; }
        SortedSet<ContainerResponse> Containers { get; }
        Dictionary<string, string> LanguageCodes { get; }
        List<FilterComparitor> NumberComparitors { get; }
        HashSet<FFmpegPreset> Presets { get; }
        SortedSet<Codec> SubtitleCodecs { get; }
        SortedSet<Encoder> SubtitleEncoders { get; }
        SortedSet<Codec> VideoCodecs { get; }
        SortedSet<Encoder> VideoEncoders { get; }
        List<string> AudioBitrates { get; }


        Task AddPresetAsync(FFmpegPreset newPreset);
        Task DeletePresetAsync(FFmpegPreset preset);
        Task<GetArgumentsResult> GetArguments(FFmpegPreset preset, WorkItem wi, CancellationToken token);
        FFmpegPreset GetPreset(string presetName);
    }
}