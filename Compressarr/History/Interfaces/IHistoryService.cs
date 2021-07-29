using Compressarr.History.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HistoryEntry = Compressarr.History.Models.HistoryEntry;

namespace Compressarr.History
{
    public interface IHistoryService
    {
        Task ClearAutoCalcResult(int mediaInfoID);
        void EndProcessing(Guid historyEntryID, bool succeeded, WorkItem workItem);
        Task<AutoCalcResult> GetAutoCalcResult(int mediaInfoID, string argument, int sampleLength);
        Task<SortedSet<MediaHistory>> GetHistory();
        Task<SortedSet<HistoryEntry>> GetProcessHistoryAsync(string filePath);
        Guid StartProcessing(WorkItem wi);
    }
}