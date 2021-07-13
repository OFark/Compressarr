using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.Shared.Models;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Interfaces
{
    public interface IMedia
    {
        public string FilePath { get; }

        public int UniqueID { get; }

        public FFProbeResponse FFProbeMediaInfo { get; set; }
        public HashSet<TreeItemData> FFProbeTreeView { get; }

        public MediaSource Source { get; set; }

        public int GetStableHash();
    }
}
