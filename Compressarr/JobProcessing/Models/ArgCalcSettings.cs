using Compressarr.Settings;

namespace Compressarr.JobProcessing.Models
{
    public class ArgCalcSettings
    {
        public bool AlwaysCalculateSSIM { get; set; }
        public int ArgCalcSampleSeconds { get; set; }
        public decimal? AutoCalculationPost { get; set; }
        public AutoCalcType AutoCalculationType { get; set; }
    }
}
