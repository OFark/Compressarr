using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IHistoryService
    {
        Task ClearAutoCalcResult(int mediaInfoID);
        void EndProcessing(Guid historyID, bool succeeded, WorkItem workItem);
        Task<AutoCalcResult> GetAutoCalcResult(int mediaInfoID, string argument, int sampleLength);
        Task<SortedSet<HistoryProcessing>> GetProcessHistoryAsync(int mediaID);
        Guid StartProcessing(int id, string filePath, Guid filterID, string preset, IEnumerable<string> arguments);
    }
}