using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Application;
using System.Threading.Tasks;
using Compressarr.Settings;

namespace Compressarr.Services
{
    public interface ISonarrService: IJobDependency
    {
        public Task<SystemStatus> TestConnection(APISettings settings);
    }
}