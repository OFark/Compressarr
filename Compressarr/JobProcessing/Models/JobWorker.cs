using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class JobWorker : IDisposable
    {
        private readonly ConditionSwitch conditionSwitch;
        public JobWorker(ConditionSwitch condition)
        {
            conditionSwitch = condition;
            condition.Start();
        }

        public void Succeed(bool ok = true)
        {
            conditionSwitch?.Complete(ok);
        }

        public void Dispose()
        {
            conditionSwitch?.Finish();
            GC.SuppressFinalize(this);
        }
    }
}
