using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.Shared.Models;
using System.Collections.Generic;

namespace Compressarr.Services.Models
{
    public class Media
    {
        public string BasePath { get; set; }

        public int UniqueID => int.Parse($"{(int)Source}{id}");

        [Filter("ID", FilterPropertyType.Number)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "From JSON source")]
        public int id { get; set; }

        public HashSet<TreeItemData> MediaHistory { get; set; }

        public FFProbeResponse FFProbeMediaInfo { get; set; }
        public HashSet<TreeItemData> FFProbeTreeView => ffProbeTreeView ??= FFProbeMediaInfo?.ToTreeItems();

        private HashSet<TreeItemData> ffProbeTreeView;

        public MediaSource Source { get; set; }
    }
}
