using Compressarr.Helpers;
using Compressarr.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.History.Models
{
    public class ProcessingHistory
    {
        public List<string> Arguments { get; set; }
        public decimal? Compression { get; set; }
        public string DestinationFilePath { get; set; }
        public Guid FilterID { get; set; }
        public decimal? FPS { get; set; }
        public int? Percentage { get; set; }
        public string Preset { get; set; }
        public decimal? Speed { get; set; }
        public decimal? SSIM { get; set; }
        public bool? Success { get; set; }
        public override string ToString()
        {
            return $"{base.ToString()} Filter: {FilterID} | Preset: {Preset} | Success: {Success?.ToString() ?? "Unknown"}{SSIM.Wrap(" | SSIM: {0}")}{Compression.Wrap(" | Comp: {0}")}{Speed.Wrap(" | Speed: {0}")}{FPS.Wrap(" | FPS: {0}")}";
        }
        

        private HashSet<TreeItemData> treeViewitems;
        public HashSet<TreeItemData> TreeViewItems => treeViewitems ??= ToTreeItemData();

        private HashSet<TreeItemData> ToTreeItemData()
        {
            var items = new HashSet<TreeItemData>()
            {
                new("Destination File", DestinationFilePath),
                new("Filter", FilterID),
                new("Preset", Preset),
                new("Success", Success?.ToString() ?? "Unknown")
            };

            if (SSIM.HasValue) items.Add(new("SSIM", SSIM.ToPercent(2).Adorn("%")));
            if (Compression.HasValue) items.Add(new("Compression", Compression.ToPercent(2).Adorn("%")));
            if (Percentage.HasValue) items.Add(new("Percentage", Percentage.Adorn("%")));
            if (Speed.HasValue) items.Add(new("Speed", Speed));
            if (FPS.HasValue) items.Add(new("FPS", FPS));
            if (Arguments != null && Arguments.Any())
            {
                var argumentsTree = new TreeItemData("Arguments")
                {
                    TreeItems = new()
                };
                for (int i = 0; i < Arguments.Count; i++)
                {
                    argumentsTree.TreeItems.Add(new($"Pass {i + 1}", Arguments[i]));
                }
                items.Add(argumentsTree);
            }

            return items;
        }

    }
}
