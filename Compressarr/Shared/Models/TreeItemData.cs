using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Shared.Models
{
    public class TreeItemData
    {

        
        public TreeItemData(string title, object value = null, HashSet<TreeItemData> subItems = null) : this(-1, title, value, null, subItems)
        { }

        public TreeItemData(string title, object value, HashSet<string> badges, HashSet<TreeItemData> subItems = null) : this(-1, title, value, badges, subItems)
        { }

        public TreeItemData(int id, string title, object value, HashSet<string> badges, HashSet<TreeItemData> subItems = null)
        {
            Badges = badges;
            Id = id;
            Title = title;
            TreeItems = subItems;
            Value = value?.ToString();
        }

        public HashSet<string> Badges { get; set; }
        public int Id { get; set; }
        public bool IsExpanded { get; set; }
        public string Title { get; set; }

        public HashSet<TreeItemData> TreeItems { get; set; }
        public string Value { get; set; }
    }
}
