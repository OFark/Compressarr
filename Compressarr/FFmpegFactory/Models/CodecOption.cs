using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class CodecOption
    {
        public string Arg { get; set; }
        public bool DisabledByVideoBitRate { get; set; }
        public bool IncludePass { get; set; }
        public int Max { get; set; }
        public int Min { get; set; }
        public string Name { get; set; }
        public CodecOptionType Type { get; set; }
        public List<string> Values { get; set; }

        public virtual bool ShouldSerializeArg() => true;
        public virtual bool ShouldSerializeDisabledByVideoBitRate() => true;
        public virtual bool ShouldSerializeIncludePass() => true;
        public virtual bool ShouldSerializeMax() => true;
        public virtual bool ShouldSerializeMin() => true;
        public virtual bool ShouldSerializeType() => true;
        public virtual bool ShouldSerializeValues() => true;
    }
}
