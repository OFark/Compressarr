using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Shared.Models
{
    public class TreeItemData
    {
        public string Title { get; set; }

        public string Value { get; set; }

        public HashSet<string> Badges { get; set; }

        public bool IsExpanded { get; set; }

        public HashSet<TreeItemData> TreeItems { get; set; }

        public TreeItemData(string title, object value = null, HashSet<TreeItemData> subItems = null): this(title, value, null, subItems)
        { }

        public TreeItemData(string title, object value, HashSet<string> badges, HashSet<TreeItemData> subItems = null)
        {
            Title = title;
            Value = value?.ToString();
            Badges = badges;
            TreeItems = subItems;
        }
    }
}
