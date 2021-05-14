using System.Collections.Generic;
using System.Linq;

namespace Compressarr.FFmpegFactory.Models
{
    public class FFmpegAudioStreamPreset
    {
        public FFmpegAudioStreamPreset()
        {
            
        }

        public FFmpegAudioStreamPreset(bool init)
        {
            Filters ??= new() { new() };
        }

        public List<FFmpegAudioStreamPresetFilter> Filters { get; set; }

        public bool CoversAny => Filters.Any(f => f.Rule == AudioStreamRule.Any);
        public AudioStreamAction Action { get; set; }
        public string BitRate { get; set; }
        public Encoder Encoder { get; set; }
    }
}
