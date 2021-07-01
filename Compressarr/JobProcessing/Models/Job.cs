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
using System.Threading;
using Compressarr.Services.Interfaces;

namespace Compressarr.JobProcessing.Models
{
    public class Job
    {
        [JsonIgnore]
        public JobCondition Condition = new();

        [JsonIgnore]
        internal CancellationTokenSource CancellationTokenSource = new();

        internal CancellationToken CancellationToken => CancellationTokenSource.Token;

        public event EventHandler StatusUpdate;

        public bool AutoImport { get; set; }

        public void Cancel()
        {
            if (CancellationToken.CanBeCanceled)
                CancellationTokenSource.Cancel();
        }

        public bool Cancelled => CancellationToken.IsCancellationRequested;
         

        public string DestinationFolder { get; set; }

        [JsonIgnore]
        public ImmutableSortedSet<JobEvent> Events { get; set; }

        [JsonIgnore]
        public Filter Filter { get; internal set; }

        [Obsolete("Depreciated in favour of FilterID")]
        public string FilterName { get; set; }
        public Guid FilterID { get; set; }

        public Guid? ID { get; set; }

        [JsonIgnore]
        public IProgress<double> InitialisationProgress { get; set; }

        [JsonIgnore]
        public bool Initialised { get; set; }
        [JsonIgnore]
        public Action<Update> LogAction { get; set; }

        public decimal? MaxCompression { get; set; }

        public decimal? MinSSIM { get; set; }

        [JsonIgnore]
        public string Name => $"{Filter?.Name}|{PresetName}";

        [JsonIgnore]
        public FFmpegPreset Preset { get; internal set; }

        public string PresetName { get; set; }

        [JsonIgnore]
        public WorkItem CurrentWorkItem { get; set; }

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
                            ConditionState.NotStarted => Condition.Prepare.State switch
                            {
                                ConditionState.NotStarted => JobState.Waiting,
                                ConditionState.Processing => JobState.Preparing,
                                ConditionState.Succeeded => JobState.Waiting,
                                ConditionState.Failed => Cancelled ? JobState.Cancelled : JobState.Error,
                                _ => throw new NotImplementedException(),
                            },
                            ConditionState.Processing => JobState.Running,
                            ConditionState.Succeeded => JobState.Waiting,
                            ConditionState.Failed => Cancelled ? JobState.Cancelled : JobState.Error,
                            _ => throw new NotImplementedException(),
                        },
                        ConditionState.Succeeded => JobState.Finished,
                        ConditionState.Failed => Cancelled ? JobState.Cancelled : JobState.Error,
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

        public void Log(Update update)
        {
            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                LogAction?.Invoke(update);

                Events = (Events ?? ImmutableSortedSet.Create<JobEvent>()).Add(new JobEvent(update));
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
                Log(new($"Job {Name} changed from {State} to {js}.", LogLevel.Information));
            }
            StatusUpdate?.Invoke(this, EventArgs.Empty);
        }

        //public void UpdateMovieInfo()
        //{
        //    InitialisationProgress?.Report(WorkLoad.Count(w => w.Movie?.MediaInfo != null) / WorkLoad.Count() * 100);

        //    StatusUpdate?.Invoke(this, EventArgs.Empty);
        //}

        public void UpdateStatus(object sender, Update update = null)
        {
            if (!string.IsNullOrWhiteSpace(update?.Message))
            {
                Log(update);
            }

            StatusUpdate?.Invoke(sender, EventArgs.Empty);
        }
    }
}