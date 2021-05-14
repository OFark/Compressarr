using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{

    public class EncoderOptionValue : EncoderOption
    {
        public int? IntValue
        {
            get
            {
                return int.TryParse(Value, out var x) ? x : null;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public string Value { get; set; }
        public override bool ShouldSerializeArg() => false;
        public override bool ShouldSerializeDisabledByVideoBitRate() => false;
        public override bool ShouldSerializeIncludePass() => false;
        public bool ShouldSerializeIntValue() => false;
        public override bool ShouldSerializeMax() => false;
        public override bool ShouldSerializeMin() => false;
        public override bool ShouldSerializeType() => false;
        public override bool ShouldSerializeValues() => false;
    }

}
