using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Application;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Compressarr.Settings;

namespace Compressarr.Services
{
    public interface IRadarrService : IJobDependency
    {
        event EventHandler OnUpdate;

        public long MovieCount { get; }

        string MovieFilter { get; set; }

        IEnumerable<string> MovieFilterValues { get; set; }

        void ClearCache();

        Task<ServiceResult<HashSet<Movie>>> GetMoviesFilteredAsync(string filter, IEnumerable<string> filterValues);

        public Task<ServiceResult<List<string>>> GetValuesForProperty(string property);

        Task<ServiceResult<object>> ImportMovie(WorkItem workItem);

        public Task<SystemStatus> TestConnection(APISettings settings);
        Task<HashSet<Movie>> GetMoviesAsync(bool force = false);
    }
}