using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class AutoPresetResult
    {
        public string ArgumentValue { get; set; }
        public bool Best { get; set; }
        public double EncodingProgress { get; set; }

        //public long OriginalSize { get; set; }
        public decimal Compression { get; set; } // => Math.Round((decimal)Size / OriginalSize * 100M, 2);

        public bool Smaller => Compression < 1;

        public bool Processing { get; set; }

        //public long Size { get; private set; }

        public decimal SSIM { get; set; }
        public double SSIMProgress { get; set; }
        public decimal Speed { get; set; }
        public void AddSize(long size, long origSize)
        {
            Compression = (decimal)size / origSize;
            //Size = size;
            //OriginalSize = origSize;
        }

        public void Reset()
        {
            Best = default;
            Compression = default;
            EncodingProgress = default;
            //Size = default;
            SSIM = default;
            SSIMProgress = default;
        }
    }
}
