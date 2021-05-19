using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory
{
    public interface IFFmpegManager : IJobDependency
    {
        SortedSet<Codec> AudioCodecs { get; }
        SortedSet<Encoder> AudioEncoders { get; }
        SortedDictionary<string, string> Containers { get; }
        Dictionary<string, string> LanguageCodes { get; }
        List<FilterComparitor> NumberComparitors { get; }
        HashSet<FFmpegPreset> Presets { get; }
        SortedSet<Codec> SubtitleCodecs { get; }
        SortedSet<Encoder> SubtitleEncoders { get; }
        SortedSet<Codec> VideoCodecs { get; }
        SortedSet<Encoder> VideoEncoders { get; }
        List<string> AudioBitrates { get; }

        Task AddPresetAsync(FFmpegPreset newPreset);
        Task<WorkItemCheckResult> CheckResult(Job job);
        Task<string> ConvertContainerToExtension(string container);
        Task DeletePresetAsync(FFmpegPreset preset);
        List<string> GetArguments(FFmpegPreset preset, FFProbeResponse mediaInfo);
        Task<FFProbeResponse> GetMediaInfoAsync(string filepath, int cacheHash = 0);
        FFmpegPreset GetPreset(string presetName);
    }
}