using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory
{
    public interface IFFmpegManager
    {
        SortedDictionary<string, string> AudioCodecs { get; }
        SortedDictionary<string, string> Containers { get; }
        HashSet<FFmpegPreset> Presets { get; }
        SortedDictionary<string, string> SubtitleCodecs { get; }
        SortedDictionary<string, string> VideoCodecs { get; }
        Task AddPresetAsync(FFmpegPreset newPreset);
        Task<WorkItemCheckResult> CheckResult(Job job);
        string ConvertContainerToExtension(string container);
        Task DeletePresetAsync(string presetName);
        Task<IMediaInfo> GetMediaInfoAsync(string filepath);
        Task<HashSet<CodecOptionValue>> GetOptionsAsync(string codec);
        FFmpegPreset GetPreset(string presetName);
        Task InitialisePreset(FFmpegPreset preset);
    }
}