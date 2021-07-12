using Compressarr.Settings;

namespace Compressarr.JobProcessing.Models
{
    public class ArgCalcSettings
    {
        public bool AlwaysCalculateSSIM { get; set; }
        public int ArgCalcSampleSeconds { get; set; }
        public decimal? AutoCalculationPost { get; set; }
        public AutoCalcType AutoCalculationType { get; set; }

        public bool VideoBitRateTargetSSIM { get; set; }
        public decimal? VideoBitRateTarget { get; set; }
    }
}
