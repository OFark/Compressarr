using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public interface ISonarrService: IJobDependency
    {
        public Task<SystemStatus> TestConnection(APISettings settings);
    }
}