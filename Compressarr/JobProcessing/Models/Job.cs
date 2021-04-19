using Compressarr.FFmpegFactory;
using Compressarr.Filtering.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.JobProcessing.Models
{
    public class Job
    {
        public event EventHandler StatusUpdate;

        public event EventHandler EndJob;

        public bool AutoImport { get; set; }
        public string BaseFolder { get; set; }

        [JsonIgnore]
        public bool Cancel { get; internal set; } = false;

        public string DestinationFolder { get; set; }

        [JsonIgnore]
        private List<JobEvent> _events = new();

        [JsonIgnore]
        public IEnumerable<JobEvent> Events
        {
            get
            {
                return _events.ToList().OrderBy(e => e.Date);
            }
        }

        [JsonIgnore]
        public Filter Filter { get; internal set; }

        public string FilterName { get; set; }

        [JsonIgnore]
        public JobState JobState { get; private set; }

        [JsonIgnore]
        public string Name => $"{FilterName}|{PresetName}";

        [JsonIgnore]
        public IFFmpegPreset Preset { get; internal set; }

        public string PresetName { get; set; }

        [JsonIgnore]
        public FFmpegProcess Process { get; internal set; }

        [JsonIgnore]
        public HashSet<WorkItem> WorkLoad { get; internal set; }

        [JsonIgnore]
        public string WriteFolder => DestinationFolder ?? BaseFolder;

        public void UpdateState(JobState state)
        {
            JobState = state;
            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }

        public void Log(string message, LogLevel level)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _events.Add(new JobEvent(level, message));
                StatusUpdate?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdateStatus(object sender, EventArgs args)
        {
            StatusUpdate?.Invoke(sender, EventArgs.Empty);
        }
    }
}