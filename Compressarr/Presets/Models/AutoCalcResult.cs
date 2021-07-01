using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class AutoCalcResult
    {
        public int Id { get; set; }
        public int UniqueID { get; set; }
        public int MediaInfoID { get; set; }
        public string Argument { get; set; }
        public decimal SSIM { get; set; }
        public decimal Speed { get; set; }
        public long Size { get; set; }
        public long OriginalSize { get; set; }

        public string SampleLength { get; set; }
    }
}
