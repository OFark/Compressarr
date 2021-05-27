namespace Compressarr.JobProcessing.Models
{
    public class JobCondition
    {

        public ConditionSwitch BuildWorkLoad { get; set; } = new();
        public bool CanCancel => Process.Processing || Prepare.Processing;
        public ConditionSwitch Encode { get; set; } = new();
        public ConditionSwitch Initialise { get; set; } = new();
        public ConditionSwitch Prepare { get; set; } = new();
        public ConditionSwitch Process { get; set; } = new();
        public bool SafeToInitialise => !Process.Processing && !Initialise.Processing & !Prepare.Processing;
        public bool SafeToRun => !Process.Started && Initialise.Succeeded;
        public ConditionSwitch Test { get; set; } = new();
        public void Clear()
        {
            BuildWorkLoad = new();
            Encode = new();
            Initialise = new();
            Prepare = new();
            Process = new();
            Test = new();
        }
    }
}
