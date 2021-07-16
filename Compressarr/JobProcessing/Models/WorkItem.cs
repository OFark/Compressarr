using Compressarr.FFmpeg.Events;
using Compressarr.Filtering;
using Compressarr.Presets.Models;
using Compressarr.Services.Interfaces;
using Compressarr.Services.Models;
using Humanizer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace Compressarr.JobProcessing.Models

{
    public class WorkItem
    {

        internal CancellationTokenSource CancellationTokenSource = new();

        public WorkItem(Movie movie, string basePath)
        {
            SourceID = movie.Id;
            MediaHash = movie.GetStableHash();
            SourceFile = movie.FilePath;
            Media = movie;
            Media.Source = MediaSource.Radarr;
        }

        public WorkItem(EpisodeFile episodeFile, string basePath)
        {
            SourceID = episodeFile.Id;
            MediaHash = episodeFile.GetStableHash();
            SourceFile = episodeFile.FilePath;
            Media = episodeFile;
            Media.Source = MediaSource.Sonarr;
        }

        public event EventHandler<Update> OnUpdate;

        public ArgumentCalculator ArgumentCalculator { get; set; }
        public IEnumerable<string> Arguments { get; set; }
        public string Bitrate { get; internal set; }
        public decimal? Compression { get; internal set; }
        public WorkItemCondition Condition { get; set; } = new();
        public ImmutableSortedSet<JobEvent> Console { get; set; }
        public string DestinationFile { get; set; }
        public string DestinationFileName => Path.GetFileName(DestinationFile);
        /// <summary>
        /// This is the Duration of the Encoding Process, not the Duration of the video
        /// </summary>
        public TimeSpan? EncodingDuration { get; internal set; }

        public TimeSpan? ETA
        {
            get
            {
                if (Percent == 100) return TimeSpan.Zero;
                if (eta.HasValue) return eta;

                if (EncodingDuration.HasValue && TotalLength.HasValue && FPS > 0)
                {
                    var percent = (decimal)EncodingDuration.Value.Ticks / TotalLength.Value.Ticks;
                    if (percent > 0)
                    {
                        return TimeSpan.FromSeconds(Convert.ToDouble(((Frame / percent) - Frame) / FPS));
                    }
                }
                return null;
            }
        }

        public decimal? FPS { get; internal set; }
        public long? Frame { get; internal set; }
        public Job Job { get; set; }
        public IMedia Media { get; set; }
        public int MediaHash { get; set; }
        public string MediaName { get; set; }
        public string Name => MediaName ?? SourceFileName;
        public int? Percent { get; internal set; }
        public decimal? Q { get; internal set; }
        public bool ShowDetails { get; set; }
        public string Size { get; internal set; }
        public string SourceFile { get; set; }
        public string SourceFileExtension => Path.GetExtension(SourceFile);
        public string SourceFileName => Path.GetFileName(SourceFile);
        public int SourceID { get; set; }
        public decimal Speed { get; internal set; }
        public decimal? SSIM { get; set; }
        public TimeSpan? TotalLength => Media.FFProbeMediaInfo?.format?.Duration;
        internal CancellationToken CancellationToken => CancellationTokenSource.Token;
        private TimeSpan? eta { get; set; }
        public void Output(string message) => Output(new(message), false);
        public void Output(Update update, bool isFFmpegProgress = false)
        {
            if (!string.IsNullOrWhiteSpace(update?.Message))
            {
                if (isFFmpegProgress)
                {
                    if (Console != null && Console.Last().IsFFmpegProgress)
                    {
                        Console = Console.Remove(Console.Last());
                    }
                    isFFmpegProgress = true;
                }

                Console = (Console ?? ImmutableSortedSet.Create<JobEvent>()).Add(new JobEvent(update, isFFmpegProgress));
            }
            Update();
        }

        public void Update()
        {
            OnUpdate?.Invoke(this, new());
        }

        public void Update(string message) => Update(new Update(message));

        public void Update(Exception ex)
        {
            var update = new Update(ex);
            OnUpdate?.Invoke(this, update);
            Output(update);
        }

        public void Update(FFmpegProgress progress)
        {
            Frame = progress.Frame;
            FPS = progress.FPS;
            Q = progress.Q;
            Size = progress.Size.Bytes().Humanize("0.00");
            Bitrate = progress.Bitrate;
            Speed = progress.Speed;
            Percent = progress.Percentage;
            EncodingDuration = progress.Time;

            if (TotalLength.HasValue && progress.FPS > 0)
            {
                var percent = (decimal)progress.Time.Ticks / TotalLength.Value.Ticks;
                if (percent > 0)
                {
                    eta = TimeSpan.FromSeconds(Convert.ToDouble(((Frame / percent) - Frame) / progress.FPS));
                }
            }

            Update();
        }

        public void Update(object _, Update update) => Update(update);

        public void Update(Update update)
        {
            OnUpdate?.Invoke(this, update);
            Output(update);
        }

        public void UpdateSSIM(FFmpegProgress progress)
        {
            Frame = progress.Frame;
            Percent = progress.Percentage;

            if (TotalLength.HasValue && progress.FPS > 0)
            {
                var percent = (decimal)progress.Time.Ticks / TotalLength.Value.Ticks;
                if (percent > 0)
                {
                    eta = TimeSpan.FromSeconds(Convert.ToDouble(((Frame / percent) - Frame) / progress.FPS));
                }
            }
            Update();
        }
    }
}