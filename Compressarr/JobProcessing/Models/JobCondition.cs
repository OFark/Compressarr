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
        public ConditionSwitch LoadMediaInfo { get; set; } = new();
        public ConditionSwitch Encode { get; set; } = new();
        public ConditionSwitch Test { get; set; } = new();
        public ConditionSwitch BuildWorkLoad { get; set; } = new();
        public void Clear()
        {
            Process = new();
            Initialise = new();
            LoadMediaInfo = new();
            Encode = new();
            Test = new();
            BuildWorkLoad = new();
        }

        public bool SafeToInitialise => !Process.Processing && !Initialise.Processing && !LoadMediaInfo.Processing;

        public bool SafeToRun => !Process.Processing && Initialise.Succeeded;

        public bool CanCancel => Process.Processing || (Initialise.Succeeded && LoadMediaInfo.Processing);
    }
}
