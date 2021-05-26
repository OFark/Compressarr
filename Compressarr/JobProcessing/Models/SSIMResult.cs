using System;

namespace Compressarr.JobProcessing.Models
{
    public class SSIMResult
    {
        public SSIMResult(bool success, decimal ssim)
        {
            SSIM = ssim;
            Success = success;
        }
        public SSIMResult(decimal ssim)
        {
            SSIM = ssim;
            Success = true;
        }

        public SSIMResult(Exception ex)
        {
            Exception = ex;
            Success = false;
        }

        public Exception Exception { get; set; }
        public decimal SSIM { get; set; }
        public bool Success { get; set; }
    }
}
