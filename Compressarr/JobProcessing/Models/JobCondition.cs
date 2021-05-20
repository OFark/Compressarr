using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class JobCondition
    {

        public ConditionSwitch Process { get; set; } = new();
        public ConditionSwitch Initialise { get; set; } = new();
        public ConditionSwitch Encode { get; set; } = new();
        public ConditionSwitch Test { get; set; } = new();
        public ConditionSwitch BuildWorkLoad { get; set; } = new();
        public void Clear()
        {
            Process = new();
            Initialise = new();
            Encode = new();
            Test = new();
            BuildWorkLoad = new();
        }

        public bool SafeToInitialise => !Process.Processing && !Initialise.Processing;

        public bool SafeToRun => !Process.Started && Initialise.Succeeded;

        public bool CanCancel => Process.Processing;
    }
}
