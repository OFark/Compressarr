using Compressarr.Presets.Models;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Compressarr.JobProcessing
{
    public interface IJobManager
    {
        HashSet<Job> Jobs { get; }
        Task<bool> AddJobAsync(Job newJob, CancellationToken token);
        void CancelJob(Job job);
        Task DeleteJob(Job job);
        bool FilterInUse(Guid id);
        Task InitialiseJob(Job job, CancellationToken token);
        void InitialiseJobs(Filter filter, CancellationToken token);
        void InitialiseJobs(MediaSource source, CancellationToken token);
        void InitialiseJobs(FFmpegPreset preset, CancellationToken token);
        Task PrepareWorkItem(WorkItem wi, FFmpegPreset preset, CancellationToken token);
        bool PresetInUse(FFmpegPreset preset);
        Task ProcessWorkItem(WorkItem wi, CancellationToken token);
        Job ReloadJob(Job job, CancellationToken token);
        void RunJob(Job job);
    }
}