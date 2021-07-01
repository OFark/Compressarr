using Compressarr.Services.Models;
using Compressarr.Settings;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Services.Base
{
    public interface IJobDependency
    {
        Task<StatusResult> GetStatus();
    }
}