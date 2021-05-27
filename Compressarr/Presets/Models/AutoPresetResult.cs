using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class AutoPresetResult
    {
        public bool Best { get; set; }

        public double EncodingProgress { get; set; }

        public decimal Percent { get; set; }

        public bool Processing { get; set; }

        public long Size { get; private set; }

        public decimal SSIM { get; set; }

        public double SSIMProgress { get; set; }

        public void AddSize(long size, long origSize)
        {
            Size = size;
            Percent = Math.Round((decimal)size / origSize * 100M, 2);
        }
    }
}
