using Compressarr.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class History
    {
        public int Id { get; set; }

        public int MediaID { get; set; }

        public List<HistoryProcessing> Entries { get; set; }

        public TreeItemData ToTreeView => new TreeItemData("ID:", Id, Entries?.Select(x => x.ToTreeView()).ToHashSet())
        {
            IsExpanded = true
        };
    }
}
