using Compressarr.JobProcessing.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public interface IApplicationInitialiser
    {
        event EventHandler<Update> OnUpdate;
        event EventHandler OnComplete;

        Task InitialiseAsync();
    }
}