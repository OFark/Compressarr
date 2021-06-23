using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IArgumentService
    {
        List<string> GetArguments(WorkItem wi);
        List<string> GetArguments(WorkItem wi, string sourceFile, string destinationFile);
        List<string> GetArgumentTemplates(WorkItem wi);
        Task SetArguments(FFmpegPreset preset, WorkItem wi, CancellationToken token);
    }
}
