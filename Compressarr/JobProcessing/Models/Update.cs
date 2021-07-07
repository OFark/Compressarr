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

        public static Update Information(string message) => new Update(message);
        public static Update Warning(string message) => new Update(message, LogLevel.Warning);
        public static Update Debug(string message) => new Update(message, LogLevel.Debug);
        public static Update Error(string message) => new Update(message, LogLevel.Error);
        public static Update FromException(Exception ex) => new Update(ex);

        public Update(Exception ex) : this(ex, LogLevel.Error) { }

        public Update(Exception ex, LogLevel level)
        {
            Message = ex?.ToString();
            Level = level;
        }

        public Update()
        {

        }

        public string Message { get; set; }
        public LogLevel Level { get; set; }
    }
}
