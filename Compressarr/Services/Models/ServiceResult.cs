using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Services.Models
{
    public class ServiceResult<T> where T : class, new()
    {
        public bool Success { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        public string ErrorString => string.Join(" - ", new List<string>() { ErrorCode, ErrorMessage }.Where(x => !string.IsNullOrWhiteSpace(x)));

        public T Results { get; set; }

        public ServiceResult(bool success, T results)
        {
            Success = success;
            Results = results;
        }

        public ServiceResult(bool success, string errorCode, string errorMessage = null, T results = default)
        {
            Success = success;
            Results = results;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}