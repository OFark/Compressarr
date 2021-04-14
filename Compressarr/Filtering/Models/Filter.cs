using System;
using System.Collections.Generic;

namespace Compressarr.Filtering.Models
{
    public class Filter
    {
        public Filter(string name, MediaSource filterType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MediaSource = filterType;
        }

        public string Name { get; set; }
        public MediaSource MediaSource { get; set; }
        public List<DynamicLinqFilter> Filters { get; set; }
    }
}