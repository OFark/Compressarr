using System;
using System.Collections.Generic;

namespace Compressarr.Filtering.Models
{
    public class Filter
    {
        public Filter()
        { }

        public Filter(string name, MediaSource filterType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MediaSource = filterType;
            ID = Guid.NewGuid();
        }

        public Guid ID { get; set; }
        public string Name { get; set; }
        public MediaSource MediaSource { get; set; }
        public List<DynamicLinqFilter> Filters { get; set; }
    }
}