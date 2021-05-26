using System;

namespace Compressarr.JobProcessing.Models
{
    public class JobWorker : IDisposable
    {
        private readonly ConditionSwitch conditionSwitch;
        private EventHandler<string> OnUpdate;
        public JobWorker(ConditionSwitch condition, EventHandler<string> updateCondition)
        {
            conditionSwitch = condition;
            condition.Start();
            OnUpdate = updateCondition;
            OnUpdate?.Invoke(this, null);
        }

        public void Dispose()
        {
            conditionSwitch?.Finish();
            GC.SuppressFinalize(this);
            OnUpdate?.Invoke(this, null);
        }

        public void Succeed(bool ok = true)
        {
            conditionSwitch?.Complete(ok);
            OnUpdate?.Invoke(this, null);
        }
    }
}
