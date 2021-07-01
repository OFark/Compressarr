using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public interface ISonarrService : IJobDependency
    {
        string SeriesFilter { get; set; }
        IEnumerable<string> SeriesFilterValues { get; set; }

        Task<ServiceResult<IEnumerable<Series>>> GetSeriesAsync(bool force = false);

        Task<ServiceResult<List<string>>> GetValuesForPropertyAsync(string property);
        Task<ServiceResult<IEnumerable<Series>>> RequestSeriesFilteredAsync(string filter, IEnumerable<string> filterValues);
        public Task<SystemStatus> TestConnectionAsync(APISettings settings);
    }
}