using Microsoft.Extensions.Logging;
using System;

namespace Compressarr.JobProcessing.Models
{
    public class Update
    {
        public Update(string message) : this(message, LogLevel.Information) { }

        public Update(string message, LogLevel level)
        {
            Message = message;
            Level = level;
        }

        public Update(Exception ex) : this(ex, LogLevel.Error) { }

        public Update(Exception ex, LogLevel level)
        {
            Message = ex.ToString();
            Level = level;
        }

        public Update()
        {

        }

        public string Message { get; set; }
        public LogLevel Level { get; set; }
    }
}
