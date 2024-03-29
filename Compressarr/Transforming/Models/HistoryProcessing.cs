﻿using Compressarr.Helpers;
using Compressarr.Shared.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class HistoryProcessing : HistoryEntry, IHistoryEntry
    {
        public List<string> Arguments { get; set; }
        public decimal? Compression { get; set; }
        public string FilePath { get; set; }
        public Guid FilterID { get; set; }
        public decimal? FPS { get; set; }
        public int? Percentage { get; set; }
        public string Preset { get; set; }
        [BsonIgnore]
        public decimal? Speed { get; set; }
        public decimal? SSIM { get; set; }
        public bool? Success { get; set; }
        public override string ToString()
        {
            return $"{base.ToString()} Filter: {FilterID} | Preset: {Preset} | Success: {Success?.ToString() ?? "Unknown"}{SSIM.Wrap(" | SSIM: {0}")}{Compression.Wrap(" | Comp: {0}")}{Speed.Wrap(" | Speed: {0}")}{FPS.Wrap(" | FPS: {0}")}";
        }

        public override TreeItemData ToTreeView()
        {
            var root = new TreeItemData(Type, Started, new()
            {
                new("Source File", FilePath),
                new("Filter", FilterID),
                new("Preset", Preset),
                new("Success", Success?.ToString() ?? "Unknown")
            });

            if (SSIM.HasValue) root.TreeItems.Add(new("SSIM", SSIM.ToPercent(2).Adorn("%")));
            if (Compression.HasValue) root.TreeItems.Add(new("Compression", Compression.ToPercent(2).Adorn("%")));
            if (Percentage.HasValue) root.TreeItems.Add(new("Percentage", Percentage.Adorn("%")));
            if (Speed.HasValue) root.TreeItems.Add(new("Speed", Speed));
            if (FPS.HasValue) root.TreeItems.Add(new("FPS", FPS));
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
                root.TreeItems.Add(argumentsTree);
            }

            return root;
        }

    }
}
