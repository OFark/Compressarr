using Compressarr.JobProcessing.Models;
using Compressarr.Shared.Models;
using LiteDB;
using System;
using System.Collections.Generic;

namespace Compressarr.History.Models
{
    public class HistoryEntry : IComparable<HistoryEntry>
    {
        public int Id { get; set; }
        public string Type { get; set; }

        public DateTime Started { get; set; }
        public DateTime? Finished { get; set; }
        public Guid HistoryID { get; set; }

        public int CompareTo(HistoryEntry other)
        {
            return Started.CompareTo(other.Started);
        }

        public ProcessingHistory ProcessingHistory { get; set; }

        [BsonIgnore]
        public bool? Success => ProcessingHistory?.Success;
        [BsonIgnore]
        public bool ShowDetails { get; set; }

        public virtual TreeItemData ToTreeView()
        {
            return new(Type, $"{Started} - {Finished}" , ProcessingHistory.TreeViewItems );
        }

        public override string ToString()
        {
            return $"{Type} [{Started}]: ";
        }
    }
}
