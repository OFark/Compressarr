using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Compressarr.JobProcessing.Models
{
    public class Job
    {
        public event EventHandler StatusUpdate;

        [JsonIgnore]
        public bool Initialised { get; set; }

        [JsonIgnore]
        public Action<LogLevel, string> LogAction { get; set; }

        public bool AutoImport { get; set; }
        [JsonIgnore]
        public bool Cancel { get; internal set; } = false;

        public string DestinationFolder { get; set; }
        [JsonIgnore]
        public ImmutableSortedSet<JobEvent> Events { get; set; }

        [JsonIgnore]
        public Filter Filter { get; internal set; }

        public string FilterName { get; set; }
        public Guid? ID { get; set; }
        [JsonIgnore]
        public JobState JobState { get; private set; }

        public decimal? MaxCompression { get; set; }
        public decimal? MinSSIM { get; set; }
        [JsonIgnore]
        public string Name => $"{FilterName}|{PresetName}";

        [JsonIgnore]
        public FFmpegPreset Preset { get; internal set; }

        public string PresetName { get; set; }

        [JsonIgnore]
        public FFmpegProcess Process { get; internal set; }

        public bool SizeCheck => AutoImport && MaxCompression.HasValue;

        [JsonIgnore]
        public bool SSIMCheck => AutoImport && MinSSIM.HasValue;
        [JsonIgnore]
        public HashSet<WorkItem> WorkLoad { get; internal set; }

        public bool SafeToInitialise => JobState switch
        {
            JobState.Finished => true,
            JobState.Running => false,
            JobState.New => true,
            JobState.Initialising => false,
            JobState.Testing => false,
            JobState.TestedFail => true,
            JobState.TestedOK => true,
            JobState.Waiting => false,
            _ => false
        };

        public void Log(string message, LogLevel level)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (LogAction != null)
                {
                    LogAction.Invoke(level, message);
                }

                Events = (Events ?? ImmutableSortedSet.Create<JobEvent>()).Add(new JobEvent(level, message));
                StatusUpdate?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdateState(JobState state)
        {
            if (JobState != state)
            {
                Log($"Job {Name} changed from {JobState} to {state}.", LogLevel.Information);
                JobState = state;
            }

            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }
        public void UpdateStatus(object sender, string message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Log(message, LogLevel.Debug);
            }

            StatusUpdate?.Invoke(sender, EventArgs.Empty);
        }
    }
}