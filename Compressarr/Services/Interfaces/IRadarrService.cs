using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public interface IRadarrService
    {
        public Task<ServiceResult<HashSet<Movie>>> GetMovies();

        public Task<ServiceResult<HashSet<Movie>>> GetMoviesFiltered(string filter, string[] filterValues);

        public ServiceResult<HashSet<Movie>> GetMoviesByJSON(string json);

        public long MovieCount { get; }

        public Task<ServiceResult<List<string>>> GetValuesForProperty(string property);

        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey);
        Task<ServiceResult<object>> ImportMovie(WorkItem workItem);
    }
}