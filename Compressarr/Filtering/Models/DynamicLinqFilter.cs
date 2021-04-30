using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Filtering.Models
{
    public class DynamicLinqFilter
    {
        public DynamicLinqFilter()
        {

        }
        public DynamicLinqFilter(FilterProperty property, FilterComparitor comparitor, string value)
        {
            Property = property;
            Comparitor = comparitor;
            Value = value;
        }

        public bool IsFirst { get; set; }

        [JsonIgnore]
        public bool IsGroup => SubFilters?.Any() ?? false;

        public string LogicalOperator { get; set; }

        public FilterProperty Property { get; set; }
        public FilterComparitor Comparitor { get; set; }
        public string Value { get; set; }

        public List<DynamicLinqFilter> SubFilters { get; set; }
    }
}