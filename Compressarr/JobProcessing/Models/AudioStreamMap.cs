using Compressarr.Presets.Models;

namespace Compressarr.JobProcessing.Models
{
    public class AudioStreamMap
    {
        public AudioStreamMap(FFmpegAudioStreamPreset preset, int streamIndex)
        {
            Preset = preset;
            StreamIndex = streamIndex;
        }

        public FFmpegAudioStreamPreset Preset { get; set; }
        public int StreamIndex { get; set; }


    }
}
