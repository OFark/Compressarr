using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IJobManager
    {
        HashSet<Job> Jobs { get; }

        Task<bool> AddJob(Job newJob);
        void CancelJob(Job job);
        Task DeleteJob(Job job);
        Task<ServiceResult<HashSet<WorkItem>>> GetFiles(Job job);
        void Init();
        Task InitialiseJob(Job job, bool force = false);
        Job ReloadJob(Job job);
        void RunJob(Job job);
    }
}