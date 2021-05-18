using Compressarr.Application.Interfaces;
using Compressarr.Settings.Filtering;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compressarr.Filtering.Models
{
    public class FilterComparitor : FilterComparitorBase, IEquatable<FilterComparitor>, ICloneable<FilterComparitor>
    {
        public static Dictionary<string, string> valueNames = new Dictionary<string, string>()
        {
            {"is", "==" },
            {"isn't", "!=" },
            {"contains", "Contains" },
            {"doesn't contain", "!Contains" },
            {"less than", "<" },
            {"more than", ">" },
            {"less than or equal", "<=" },
            {"more than or equal", ">=" }
        };

        public FilterComparitor()
        {

        }

        public FilterComparitor(string value)
        {
            Value = value;
        }

        [JsonIgnore]
        public bool IsParamMethod => new Regex(@"\w").IsMatch(Value);

        [JsonIgnore]
        public string Key
        {
            get
            {
                KeyValuePair<string, string> def = default;
                var kpvalue = valueNames.FirstOrDefault(x => x.Value == Value);
                if (!kpvalue.Equals(def))
                {
                    return kpvalue.Key;
                }

                return Value;
            }
        }

        [JsonIgnore]
        public bool Not => Value.StartsWith("!");

        [JsonIgnore]
        public string Operator => $"{(IsParamMethod ? "." : " ")}{Value}{(IsParamMethod ? "(@)" : " ")}";

        public FilterComparitor Clone()
        {
            return new(Value);
        }

        public bool Equals(FilterComparitor other)
        {   
            return other != null && Value == other.Value;
        }

        public override string ToString()
        {
            return Key;
        }
    }
}