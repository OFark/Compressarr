using Compressarr.Shared.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.History.Models
{
    public class MediaHistory : IComparable<MediaHistory>
    {
        public int Id { get; set; }

        public string FilePath { get; set; }

        public SortedSet<HistoryEntry> Entries { get; set; }

        [BsonIgnore]
        public TreeItemData ToTreeView() => new(Entries?.Max(x => x.Started).ToString(), FilePath, Entries?.Reverse().Select(x => x.ToTreeView()).ToHashSet());
        
        [BsonIgnore]
        public bool ShowDetails { get; set; }

        public int CompareTo(MediaHistory other)
        {
            return Entries?.Max(x => x.Started).CompareTo(other?.Entries?.Max(x => x.Started)) ?? FilePath.CompareTo(other.FilePath);
        }
    }
}
