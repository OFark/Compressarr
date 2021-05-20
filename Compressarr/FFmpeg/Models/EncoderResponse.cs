using Compressarr.Presets;

namespace Compressarr.FFmpeg.Models
{
    public class EncoderResponse
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public CodecType Type { get; set; }
    }
}
