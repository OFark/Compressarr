using Compressarr.FFmpegFactory.Models;
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
        SortedDictionary<string, string> Containers { get; }
        HashSet<FFmpegPreset> Presets { get; }
        SortedSet<Codec> SubtitleCodecs { get; }
        SortedSet<Codec> VideoCodecs { get; }
        Task AddPresetAsync(FFmpegPreset newPreset);
        Task<WorkItemCheckResult> CheckResult(Job job);
        string ConvertContainerToExtension(string container);
        Task DeletePresetAsync(FFmpegPreset preset);
        Codec GetCodec(CodecType type, string name);
        Task<IMediaInfo> GetMediaInfoAsync(string filepath);
        FFmpegPreset GetPreset(string presetName);
    }
}