using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class EncoderOption
    {
        public string Arg { get; set; }
        public bool DisabledByVideoBitRate { get; set; }
        public bool IncludePass { get; set; }
        public int Max { get; set; }
        public int Min { get; set; }
        public string Name { get; set; }
        public CodecOptionType Type { get; set; }
        public List<string> Values { get; set; }
    }
}
