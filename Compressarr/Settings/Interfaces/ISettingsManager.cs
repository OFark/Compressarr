using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public interface ISettingsManager
    {
        Dictionary<CodecType, SortedSet<Codec>> Codecs { get; set; }
        SortedDictionary<string, string> Containers { get; set; }
        HashSet<Filter> Filters { get; set; }
        HashSet<Job> Jobs { get; set; }
        HashSet<FFmpegPreset> Presets { get; }
        APISettings RadarrSettings { get; set; }
        APISettings SonarrSettings { get; set; }

        Task DumpDebugFile(string fileName, string content);
        LogLevel GetLogLevel();
        Task<T> ReadJsonFileAsync<T>(AppFile file) where T : class;
        Task<T> ReadJsonFileAsync<T>(string path) where T : class;
        Task<string> ReadTextFileAsync(AppFile file);
        Task<string> ReadTextFileAsync(string path);
        Task SaveAppSetting();
        Task UpdateLogLevel(LogLevel level);
        Task WriteJsonFileAsync(AppFile file, object content);
        Task WriteJsonFileAsync(string path, object content);
        Task WriteTextFileAsync(AppFile file, string content);
        Task WriteTextFileAsync(string path, string content);
    }
}