using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Interfaces;
using Compressarr.Filtering.Models;
using Newtonsoft.Json;
using System;
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
        private Dictionary<DateTime, string> _events = new Dictionary<DateTime, string>();

        [JsonIgnore]
        private object eventLock = new object();

        [JsonIgnore]
        public Dictionary<DateTime, string> Events
        {
            get
            {
                lock (eventLock)
                {
                    return _events;
                }
            }
        }

        [JsonIgnore]
        public Filter Filter { get; internal set; }

        public string FilterName { get; set; }

        [JsonIgnore]
        public JobStatus JobStatus { get; internal set; }

        [JsonIgnore]
        public JobTester JobTester { get; internal set; }

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

        public void EndOfJob(bool success)
        {
            lock (eventLock)
            {
                _events.Add(DateTime.Now, $"Job Finished: {(success ? "Success" : "Failed")}");
            }

            JobStatus = JobStatus.Finished;
            EndJob?.Invoke(this, EventArgs.Empty);
            UpdateStatus("");
        }

        public void UpdateStatus(params string[] messages)
        {
            lock (eventLock)
            {
                foreach (var m in messages.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    _events.Add(DateTime.Now, m);
                }
            }
            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateStatus(object sender, EventArgs args)
        {
            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }
    }
}