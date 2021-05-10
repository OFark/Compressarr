using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public interface IRadarrService : IJobDependency
    {
        public Task<ServiceResult<HashSet<Movie>>> GetMoviesAsync();

        public Task<ServiceResult<HashSet<Movie>>> GetMoviesFilteredAsync(string filter, string[] filterValues);

        public long MovieCount { get; }

        public Task<ServiceResult<List<string>>> GetValuesForProperty(string property);

        public Task<SystemStatus> TestConnection(APISettings settings);
        void ClearCache();
        Task<ServiceResult<object>> ImportMovie(WorkItem workItem);
    }
}