using Compressarr.Shared.Models;
using System;

namespace Compressarr.JobProcessing.Models
{
    public interface IHistoryEntry : IComparable<IHistoryEntry>
    {
        string Type { get; set; }
        DateTime Started { get; set; }
        DateTime? Finished { get; set; }
        Guid HistoryID { get; set; }
        int Id { get; set; }

        TreeItemData ToTreeView();
    }
}