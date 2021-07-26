using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.FFmpeg.Models
{
    public class FFResult : FFResult<object>
    {
        public FFResult(bool success) : base(success, null)
        {
            Success = success;
            ReceivedAt = DateTime.Now;
        }

        public FFResult(Exception ex) : base(ex)
        {}
    }
    public class FFResult<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }

        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime ReceivedAt { get; init; }

        public Exception Exception { get; set; }

        public string ErrorString => string.Join(" - ", new List<string>() { ErrorCode.ToString(), ErrorMessage }.Where(x => !string.IsNullOrWhiteSpace(x)));

        public IEnumerable<T> Results { get; set; }

        public FFResult(bool success, T result)
        {
            Success = success;
            Result = result;
            ReceivedAt = DateTime.Now;
        }

        public FFResult(bool success, IEnumerable<T> results)
        {
            Success = success;
            Results = results;
            ReceivedAt = DateTime.Now;
        }

        public FFResult(ProcessResponse failedResponse)
        {
            Success = false;
            Result = default;
            Results = default;
            ErrorCode = failedResponse.ExitCode;
            ErrorMessage = failedResponse.StdErr ?? failedResponse.StdOut;
        }

        public FFResult(Exception ex)
        {
            Success = false;
            Result = default;
            Results = default;
            ErrorCode = ex.HResult;
            ErrorMessage = ex.ToString();
            Exception = ex;
        }

        public FFResult(bool success, int errorCode, string errorMessage = null, T result = default, IEnumerable<T> results = default)
        {
            Success = success;
            Result = result;
            Results = results;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ReceivedAt = DateTime.Now;
        }
    }
}
