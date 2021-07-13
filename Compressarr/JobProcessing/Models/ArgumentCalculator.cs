using Compressarr.FFmpeg.Models;
using Compressarr.Presets.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class ArgumentCalculator
    {
        public Task GenSamplesTask = null;

        public ArgumentCalculator(WorkItem wi, FFmpegPreset preset)
        {
            if (wi?.Media?.FFProbeMediaInfo == null) throw new ArgumentException("MediaInfo is not available");

            AudioStreams = wi.Media.FFProbeMediaInfo.AudioStreams ?? new HashSet<Stream>();
            VideoStreams = wi.Media.FFProbeMediaInfo.VideoStreams ?? new HashSet<Stream> ();

            Preset = preset;

            ColorPrimaries = wi.Media.FFProbeMediaInfo.VideoStreams?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.color_primaries))?.color_primaries;
            ColorTransfer = wi.Media.FFProbeMediaInfo.VideoStreams?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.color_transfer))?.color_transfer;

            VideoEncoderOptions = new();
            foreach(var veo in preset.VideoEncoderOptions)
            {
                var eov = new EncoderOptionValue(veo.Name)
                {
                    AutoCalculate = veo.AutoCalculate,
                    EncoderOption = veo.EncoderOption,
                    Value = veo.Value
                };
                VideoEncoderOptions.Add(eov);
            }
        }

        public IEnumerable<Stream> AudioStreams { get; set; }
        public IEnumerable<EncoderOptionValue> AutoCalcVideoEncoderOptions => VideoEncoderOptions?.Where(x => x.AutoCalculate);
        public string ColorPrimaries { get; set; }
        public string ColorTransfer { get; set; }
        public FFmpegPreset Preset { get; set; }
        public int SampleSize { get; set; }
        public bool TwoPass => (Preset?.VideoBitRate.HasValue ?? false) || (Preset?.VideoBitRateAutoCalc ?? false);
        public VideoBitRateCalculator VideoBitRateCalculator { get; set; }
        public HashSet<EncoderOptionValue> VideoEncoderOptions { get; set; }
        public IEnumerable<Stream> VideoStreams { get; set; }
    }
}
