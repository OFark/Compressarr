using Compressarr.Presets;
using Compressarr.Presets.Models;
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
        [JsonIgnore]
        public JobCondition Condition = new();

        public event EventHandler StatusUpdate;

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
        public IProgress<double> InitialisationProgress { get; set; }

        [JsonIgnore]
        public bool Initialised { get; set; }
        [JsonIgnore]
        public Action<LogLevel, string> LogAction { get; set; }

        //var r = State switch
        //{
        //    JobState.BuildingWorkLoad => throw new NotImplementedException(),
        //    JobState.Cancelled => throw new NotImplementedException(),
        //    JobState.Error => throw new NotImplementedException(),
        //    JobState.Finished => throw new NotImplementedException(),
        //    JobState.Initialising => throw new NotImplementedException(),
        //    JobState.LoadingMediaInfo => throw new NotImplementedException(),
        //    JobState.New => throw new NotImplementedException(),
        //    JobState.Ready => throw new NotImplementedException(),
        //    JobState.Running => throw new NotImplementedException(),
        //    JobState.TestedFail => throw new NotImplementedException(),
        //    JobState.TestedOK => throw new NotImplementedException(),
        //    JobState.Testing => throw new NotImplementedException(),
        //    JobState.Waiting => throw new NotImplementedException(),
        //    _ => throw new NotImplementedException(),
        //}
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
        public JobState State => Condition.Initialise.State switch
        {
            ConditionState.NotStarted => JobState.New,
            ConditionState.Processing => Condition.Test.State switch
            {
                ConditionState.NotStarted => JobState.Initialising,
                ConditionState.Processing => Condition.BuildWorkLoad.State switch
                {
                    ConditionState.NotStarted => JobState.Testing,
                    ConditionState.Processing => JobState.BuildingWorkLoad,
                    ConditionState.Succeeded => JobState.Testing,
                    ConditionState.Failed => JobState.Error,
                    _ => throw new NotImplementedException()
                },
                ConditionState.Succeeded => JobState.TestedOK,
                ConditionState.Failed => JobState.Error,
                _ => throw new NotImplementedException(),
            },
            ConditionState.Succeeded => Condition.Test.State switch
            {
                ConditionState.NotStarted => JobState.Error,
                ConditionState.Processing => JobState.Error,
                ConditionState.Succeeded => Condition.BuildWorkLoad.State switch
                {
                    ConditionState.NotStarted => JobState.Error,
                    ConditionState.Processing => JobState.Error,
                    ConditionState.Succeeded => Condition.Process.State switch
                    {
                        ConditionState.NotStarted => JobState.Ready,
                        ConditionState.Processing => Condition.Encode.State switch
                        {
                            ConditionState.NotStarted => JobState.Waiting,
                            ConditionState.Processing => JobState.Running,
                            ConditionState.Succeeded => JobState.Waiting,
                            ConditionState.Failed => Cancel ? JobState.Cancelled : JobState.Error,
                            _ => throw new NotImplementedException(),
                        },
                        ConditionState.Succeeded => JobState.Finished,
                        ConditionState.Failed => Cancel ? JobState.Cancelled : JobState.Error,
                        _ => throw new NotImplementedException(),
                    },
                    ConditionState.Failed => JobState.Error,
                    _ => throw new NotImplementedException(),
                },
                ConditionState.Failed => JobState.Error,
                _ => throw new NotImplementedException(),
            },
            ConditionState.Failed => JobState.Error,
            _ => throw new NotImplementedException()
        };
        [JsonIgnore]
        public HashSet<WorkItem> WorkLoad { get; internal set; }

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

        public override string ToString()
        {
            return Name;
        }
        public void UpdateCondition(Action<JobCondition> action)
        {
            var js = State;
            action.Invoke(Condition);
            if (State != js)
            {
                Log($"Job {Name} changed from {State} to {js}.", LogLevel.Information);
            }
            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateMovieInfo()
        {
            InitialisationProgress?.Report(WorkLoad.Count(w => w.Movie?.MediaInfo != null) / WorkLoad.Count() * 100);

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