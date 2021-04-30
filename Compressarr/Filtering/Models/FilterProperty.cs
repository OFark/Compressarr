using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Filtering.Models
{
    public class FilterProperty
    {
        public FilterProperty()
        {

        }
        public FilterProperty(string name, string value, FilterPropertyType propertyType, string suffix = null, string filterOn = null)
        {
            Key = name;
            Value = value;
            PropertyType = propertyType;
            Suffix = suffix;
            FilterOn = filterOn;
        }

        public string Key { get; set; }
       
        [JsonIgnore]
        public string Name => Key.Split(" - ").LastOrDefault();

        public string Value { get; set; }

        [JsonIgnore]
        public string Suffix { get; set; }

        [JsonIgnore]
        public string FilterOn { get; set; }

        public FilterPropertyType PropertyType { get; set; }
    }
}