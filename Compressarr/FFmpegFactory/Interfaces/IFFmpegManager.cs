using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Compressarr.FFmpegFactory
{
    public interface IFFmpegManager
    {
        SortedDictionary<string, string> AudioCodecs { get; }
        SortedDictionary<string, string> Containers { get; }
        HashSet<IFFmpegPreset> Presets { get; }
        FFmpegStatus Status { get; }
        SortedDictionary<string, string> SubtitleCodecs { get; }
        SortedDictionary<string, string> VideoCodecs { get; }

        void AddPreset(IFFmpegPreset newPreset);
        Task<bool> CheckResult(WorkItem workitem);
        string ConvertContainerToExtension(string container);
        void DeletePreset(string presetName);
        string GetFFmpegVersion();
        Task<IMediaInfo> GetMediaInfo(string filepath);
        HashSet<CodecOptionValue> GetOptions(string codec);
        IFFmpegPreset GetPreset(string presetName);
        void Init();
    }
}