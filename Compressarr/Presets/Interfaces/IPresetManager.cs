using Compressarr.FFmpeg.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using Compressarr.Services.Base;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Presets
{
    public interface IPresetManager : IJobDependency
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
        Task<string> ConvertContainerToExtension(string container);
        Task DeletePresetAsync(FFmpegPreset preset);
        Task<List<string>> GetArguments(FFmpegPreset preset, FFProbeResponse mediaInfo);
        FFmpegPreset GetPreset(string presetName);
    }
}