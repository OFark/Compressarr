using System.Collections.Generic;

namespace Compressarr.Presets.Models
{
    public class EncoderAutoTune
    {
        public int End { get; set; }
        public int Start { get; set; }
        public List<string> Values { get; set; }
    }
}
