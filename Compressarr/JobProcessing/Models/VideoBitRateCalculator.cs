using Compressarr.Presets.Models;
using System.Collections.Generic;

namespace Compressarr.JobProcessing.Models
{
    public class VideoBitRateCalculator
    {

        public VideoBitRateCalculator()
        {
            SampleResults = new();
        }
        public int CurrentBitrate { get; set; }
        public int OriginalBitrate { get; set; }

        public List<AutoPresetResult> SampleResults { get; set; }
    }
}
