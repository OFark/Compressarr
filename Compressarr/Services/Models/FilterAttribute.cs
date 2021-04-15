using Compressarr.Filtering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    public class FilterAttribute : Attribute
    {
        public virtual string FilterOn { get; set; }
        public virtual string Name { get; set; }
        public virtual FilterPropertyType PropertyType { get; set; }
        public virtual string Suffix { get; set; }
        public virtual bool Traverse { get; set; }

        public FilterAttribute(string name, FilterPropertyType propertyType = FilterPropertyType.String)
        {
            Name = name;
            PropertyType = propertyType;
        }

        public FilterAttribute(string name, bool traverse)
        {
            Name = name;
            Traverse = traverse;
        }
    }
}
