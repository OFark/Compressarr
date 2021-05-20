using System;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Presets
{
    public interface IApplicationInitialiser
    {
        Task InitialiseAsync(CancellationToken cancellationToken = default);
    }
}