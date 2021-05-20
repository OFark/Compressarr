using Compressarr.Presets;

namespace Compressarr.FFmpeg.Models
{
    public class CodecResponse
    {
        public string Description { get; set; }
        public bool IsDecoder { get; set; }
        public bool IsEncoder { get; set; }
        public string Name { get; set; }
        public CodecType Type { get; set; }
    }
}
