using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compressarr.Filtering.Models
{
    public class FilterComparitor
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

        public FilterComparitor(string value)
        {
            Value = value;
        }

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

        public string Value { get; set; }

        [JsonIgnore]
        public bool IsParamMethod
        {
            get
            {
                var reg = new Regex(@"\w");
                return reg.IsMatch(Value);
            }
        }

        [JsonIgnore]
        public bool Not => Value.StartsWith("!");

        [JsonIgnore]
        public string Operator => $"{(IsParamMethod ? "." : " ")}{Value}{(IsParamMethod ? "(@)" : " ")}";
    }
}