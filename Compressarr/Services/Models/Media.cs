using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.Services.Interfaces;
using Compressarr.Shared.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Compressarr.Services.Models
{
    public class Media : IMedia
    {
        public string BasePath { get; set; }

        public int UniqueID => int.Parse($"{(int)Source}{Id}");

        [Filter("ID", FilterPropertyType.Number)]
        [JsonProperty("id")]
        public int Id { get; set; }

        public HashSet<TreeItemData> MediaHistory { get; set; }

        public FFProbeResponse FFProbeMediaInfo { get; set; }
        public HashSet<TreeItemData> FFProbeTreeView => ffProbeTreeView ??= FFProbeMediaInfo?.ToTreeItems();

        private HashSet<TreeItemData> ffProbeTreeView;

        public MediaSource Source { get; set; }
        public string FilePath { get; internal set; }

        public int GetStableHash()
        {
            return JsonConvert.SerializeObject(this).GetStableHashCode();
        }
    }
}
