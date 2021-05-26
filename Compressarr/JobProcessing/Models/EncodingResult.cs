using System;

namespace Compressarr.JobProcessing.Models
{
    public class EncodingResult
    {
        public bool Success { get; set; }
        public Exception Exception { get; set; }

        public EncodingResult(bool success)
        {
            Success = success;
        }

        public EncodingResult(Exception ex)
        {
            Exception = ex;
            Success = false;
        }

    }
}
