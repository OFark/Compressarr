using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application.Interfaces
{
    public interface IStartupTask
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
