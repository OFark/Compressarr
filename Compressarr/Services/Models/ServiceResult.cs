using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Compressarr.Services.Models
{
    public class ServiceResult<T> where T : class, new()
    {
        public bool Success { get; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime ReceivedAt { get; }
        public TimeSpan Expires { get; }


        public string ErrorString => string.Join(" - ", new List<string>() { ErrorCode, ErrorMessage }.Where(x => !string.IsNullOrWhiteSpace(x)));

        public T Results { get; set; }

        public ServiceResult(bool success, T results, TimeSpan? expires = null)
        {
            Success = success;
            Results = results;
            ReceivedAt = DateTime.Now;
            Expires = expires ?? Timeout.InfiniteTimeSpan;
        }

        public ServiceResult(bool success, string errorCode, string errorMessage = null, T results = default)
        {
            Success = success;
            Results = results;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ReceivedAt = DateTime.Now;
        }

        public bool HasExpired => !Success || (Expires != Timeout.InfiniteTimeSpan && (DateTime.Now - ReceivedAt > Expires));
    }
}