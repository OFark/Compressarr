using System.Threading.Tasks;

namespace Compressarr.Services.Base
{
    public interface IJobDependency
    {
        Task<StatusResult> GetStatus();
    }
}