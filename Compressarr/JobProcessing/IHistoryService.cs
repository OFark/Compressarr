using Compressarr.JobProcessing.Models;
using System;
using System.Collections.Generic;

namespace Compressarr.JobProcessing
{
    public interface IHistoryService
    {
        void EndProcessing(Guid historyID, bool succeeded, WorkItem workItem);
        SortedSet<HistoryProcessing> GetProcessHistory(int mediaID);
        Guid StartProcessing(int id, string filePath, string filter, string preset, IEnumerable<string> arguments);
    }
}