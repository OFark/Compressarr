namespace Compressarr.JobProcessing.Models
{
    public class JobCondition
    {

        public ConditionSwitch BuildWorkLoad { get; set; } = new();
        public bool CanCancel => Process.Processing;
        public ConditionSwitch Initialise { get; set; } = new();
        
        public ConditionSwitch Process { get; set; } = new();
        public bool SafeToInitialise => !Process.Processing && !Initialise.Processing;
        public bool SafeToRun => !Process.Started && Initialise.Succeeded;
        public ConditionSwitch Test { get; set; } = new();
        public void Clear()
        {
            BuildWorkLoad = new();
            Initialise = new();
            Process = new();
            Test = new();
        }
    }
}
