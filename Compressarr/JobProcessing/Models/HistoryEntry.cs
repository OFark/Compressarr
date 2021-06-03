using Compressarr.Shared.Models;
using System;
using System.Collections.Generic;

namespace Compressarr.JobProcessing.Models
{
    public abstract class HistoryEntry : IHistoryEntry
    {
        public int Id { get; set; }
        public string Type { get; set; }

        public DateTime Started { get; set; }
        public DateTime? Finished { get; set; }
        public Guid HistoryID { get; set; }

        public int CompareTo(IHistoryEntry other)
        {
            return Started.CompareTo(other.Started);
        }

        public virtual TreeItemData ToTreeView()
        {
            return new(Type, Started.ToString());
        }

        public override string ToString()
        {
            return $"{Type} [{Started}]: ";
        }
    }
}
