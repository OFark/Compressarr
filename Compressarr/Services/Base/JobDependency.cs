using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Base
{
    public abstract class JobDependency : IJobDependency
    {
        public abstract Task<StatusResult> GetStatus();
    }

}
