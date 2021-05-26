using Compressarr.Filtering;
using Compressarr.Presets.Models;
using Compressarr.Services.Models;
using System;
using System.Collections.Generic;
using System.IO;

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

        public event EventHandler<string> OnUpdate;

        public void Update(string message = null)
        {
            OnUpdate?.Invoke(this, message);
        }

        public List<AutoPresetTest> ArgCalcResults { get; set; }
        public Movie Movie { get; set; }
        public IEnumerable<string> Arguments { get; set; }
        public string Bitrate { get; internal set; }
        public decimal? Compression { get; internal set; }
        public string DestinationFile { get; set; }
        public string DestinationFileName => Path.GetFileName(DestinationFile);
        public TimeSpan? Duration { get; internal set; }
        public bool Finished { get; internal set; } = false;
        public decimal? FPS { get; internal set; }
        public long? Frame { get; internal set; }
        public int MediaHash { get; set; }
        public string MovieName { get; set; }
        public string Name => MovieName ?? SourceFileName;
        public int? Percent { get; internal set; }
        public decimal? Q { get; internal set; }
        public bool Running { get; internal set; } = false;
        public bool ShowArgs { get; set; }
        public bool CalcBest { get; set; }
        public string Size { get; internal set; }
        public MediaSource Source { get; set; }
        public string SourceFile { get; set; }
        public string SourceFileName => Path.GetFileName(SourceFile);
        public int SourceID { get; set; }
        public string Speed { get; internal set; }
        public decimal? SSIM { get; set; }
        public bool Success { get; internal set; } = false;
        public TimeSpan? TotalLength => Movie?.MediaInfo?.format?.Duration; //WorkItem Duration is the current process time frame, MediaInfo Duration is the movie length.
    }
}