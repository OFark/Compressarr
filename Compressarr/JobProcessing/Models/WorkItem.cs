using Compressarr.Filtering;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Compressarr.JobProcessing.Models

{
    public class WorkItem
    {

        public WorkItem(Movie movie, string basePath)
        {
            SourceID = movie.id;
            Source = MediaSource.Radarr;
            MediaHash = movie.GetStableHash();
            SourceFile = $"{basePath}{Path.Combine(movie.path, movie.movieFile.relativePath)}";
            Movie = movie;
        }

        public event EventHandler<Update> OnUpdate;

        public List<AutoPresetTest> ArgCalcResults { get; set; }
        public IEnumerable<string> Arguments { get; set; }
        public string Bitrate { get; internal set; }
        public bool CalcBest { get; set; }
        public decimal? Compression { get; internal set; }
        public ImmutableSortedSet<JobEvent> Console { get; set; }
        public string DestinationFile { get; set; }
        public string DestinationFileName => Path.GetFileName(DestinationFile);
        public TimeSpan? Duration { get; internal set; }
        public bool Finished { get; internal set; } = false;
        public decimal? FPS { get; internal set; }
        public long? Frame { get; internal set; }
        public Job Job { get; set; }
        public int MediaHash { get; set; }

        public Movie Movie { get; set; }

        public string MovieName { get; set; }

        public string Name => MovieName ?? SourceFileName;

        public int? Percent { get; internal set; }

        public decimal? Q { get; internal set; }

        public bool ShowArgs { get; set; }

        public string Size { get; internal set; }

        public MediaSource Source { get; set; }

        public string SourceFile { get; set; }

        public string SourceFileName => Path.GetFileName(SourceFile);

        public int SourceID { get; set; }

        public string Speed { get; internal set; }

        public decimal? SSIM { get; set; }

        public bool Success { get; internal set; } = false;

        public TimeSpan? TotalLength => Movie?.MediaInfo?.format?.Duration;

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
        public void Update(Update update)
        {
            OnUpdate?.Invoke(this, update);
            Output(update);
        }
        //WorkItem Duration is the current process time frame, MediaInfo Duration is the movie length.
    }
}