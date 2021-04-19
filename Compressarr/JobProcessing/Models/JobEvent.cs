using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class JobEvent
    {
        public DateTime Date { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public JobEvent(LogLevel level, string message)
        {
            Date = DateTime.Now;
            Level = level;
            Message = message;
        }

        public JobEvent(string message)
        {
            Date = DateTime.Now;
            Level = LogLevel.Information;
            Message = message;
        }
    }
}
