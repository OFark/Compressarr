using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IJobManager
    {
        HashSet<Job> Jobs { get; }

        Task<bool> AddJobAsync(Job newJob);
        void CancelJob(Job job);
        Task DeleteJob(Job job);
        bool FilterInUse(string filterName);
        Task<ServiceResult<HashSet<WorkItem>>> GetFiles(Job job);
        Task InitialiseJob(Job job, bool force = false);
        bool PresetInUse(FFmpegPreset preset);
        Job ReloadJob(Job job);
        void RunJob(Job job);
        void Stop(Job job);
    }
}