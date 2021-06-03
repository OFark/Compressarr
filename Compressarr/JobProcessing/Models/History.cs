using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class History
    {
        public int Id { get; set; }

        public string FilePath { get; set; }

        public List<IHistoryEntry> Entries { get; set; }
    }
}
