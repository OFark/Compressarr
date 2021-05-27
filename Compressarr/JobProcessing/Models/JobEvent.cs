using Microsoft.Extensions.Logging;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class JobEvent : IComparable<JobEvent>
    {
        public DateTime Date { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public bool IsFFmpegProgress { get; set; }

        public Color Color=> Level switch
                {
                    LogLevel.Information => Color.Info,
                    LogLevel.Warning => Color.Warning,
                    LogLevel.Error or LogLevel.Critical => Color.Error,
                    _ => Color.Default,
                };

        public JobEvent(Update update, bool isFFmpegProgress)
        {
            Date = DateTime.Now;
            Level = update.Level;
            Message = update.Message;
            IsFFmpegProgress = isFFmpegProgress;
        }

        public JobEvent(Update update) : this(update, false) { }

        public int CompareTo(JobEvent other)
        {
            return Date.CompareTo(other.Date);
        }
    }
}
