using Compressarr.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.JobProcessing.Models
{
    public class History : IComparable<History>
    {
        public int Id { get; set; }

        public int MediaID { get; set; }

        public List<HistoryProcessing> Entries { get; set; }

        public TreeItemData ToTreeView => new ("ID:", Id, Entries?.Select(x => x.ToTreeView()).ToHashSet())
        {
            IsExpanded = true
        };

        public int CompareTo(History other)
        {
            return Entries?.Max(x => x.Started).CompareTo(other.Entries?.Max(x => x.Started)) ?? MediaID.CompareTo(other.MediaID);
        }
    }
}
