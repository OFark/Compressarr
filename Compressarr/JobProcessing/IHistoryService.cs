using Compressarr.JobProcessing.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IHistoryService
    {
        void EndProcessing(Guid historyID, bool succeeded, WorkItem workItem);
        SortedSet<IHistoryEntry> GetHistory(string filePath);
        Guid StartProcessing(string filePath, string filter, string preset, IEnumerable<string> arguments);
    }
}