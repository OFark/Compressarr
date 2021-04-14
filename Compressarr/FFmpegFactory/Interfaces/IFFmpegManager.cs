using Compressarr.FFmpegFactory.Models;
using Compressarr.JobProcessing;
using System.Collections.Generic;

namespace Compressarr.FFmpegFactory.Interfaces
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
        bool CheckResult(WorkItem workitem);
        string ConvertContainerToExtension(string container);
        void DeletePreset(string presetName);
        HashSet<CodecOptionValue> GetOptions(string codec);
        IFFmpegPreset GetPreset(string presetName);
        void Init();
    }
}