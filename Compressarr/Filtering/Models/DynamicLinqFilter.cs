using Compressarr.Application;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Filtering.Models
{
    public class DynamicLinqFilter : ICloneable<DynamicLinqFilter>
    {
        public DynamicLinqFilter()
        {

        }

        public DynamicLinqFilter(FilterProperty property, FilterComparitor comparitor, string value, IEnumerable<string> values = null)
        {
            Property = property;
            Comparitor = comparitor;
            Value = value;
            Values = values;
        }

        public bool IsFirst { get; set; }

        [JsonIgnore]
        public bool IsGroup => SubFilters?.Any() ?? false;

        [JsonIgnore]
        public decimal ValueNum
        {
            get
            {
                return decimal.TryParse(Value, out var x) ? x : 0;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public string LogicalOperator { get; set; }

        public FilterProperty Property { get; set; }
        public FilterComparitor Comparitor { get; set; }
        public string Value { get; set; }
        public IEnumerable<string> Values { get; set; }

        public List<DynamicLinqFilter> SubFilters { get; set; }

        public DynamicLinqFilter Clone()
        {
            return new DynamicLinqFilter() { Comparitor = Comparitor, Property = Property, Value = Value, Values = Values };
        }
    }
}