using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public class AppSettings
    {
        public bool AlwaysCalculateSSIM { get; set; }
        public int ArgCalcSampleSeconds { get; set; }
        public decimal? AutoCalculationPost { get; set; }
        public AutoCalcType AutoCalculationType { get; set; }
    }
}
