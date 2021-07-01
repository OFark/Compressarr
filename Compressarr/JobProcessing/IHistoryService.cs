using Compressarr.JobProcessing.Models;
using System;
using System.Collections.Generic;

namespace Compressarr.JobProcessing
{
    public interface IHistoryService
    {
        void EndProcessing(Guid historyID, bool succeeded, WorkItem workItem);
        SortedSet<HistoryProcessing> GetProcessHistory(int mediaID);
        Guid StartProcessing(int id, string filePath, Guid filterID, string preset, IEnumerable<string> arguments);
    }
}