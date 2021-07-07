namespace Compressarr.JobProcessing.Models
{
    public class WorkItemCondition
    {
        public ConditionSwitch Analyse { get; set; } = new();
        /// <summary>
        /// Is currently processing
        /// </summary>
        public bool CanCancel => Processing.Processing;
        /// <summary>
        /// Either Encoding or Analysing output
        /// </summary>
        public bool CreatingVideo => Encode.Processing || Analyse.Processing;

        public ConditionSwitch Encode { get; set; } = new();
        /// <summary>
        /// Both Encode and Analyse were successful
        /// </summary>
        public bool HappyEncode => Encode.Succeeded && Analyse.Succeeded;
        /// <summary>
        /// Processing has finished
        /// </summary>
        public bool HasFinished => Processing.Finished;
        public ConditionSwitch Import { get; set; } = new();
        public ConditionSwitch OutputCheck { get; set; } = new();
        public ConditionSwitch Prepare { get; set; } = new();
        public ConditionSwitch Processing { get; set; } = new();
        /// <summary>
        /// Output Check succeeded
        /// </summary>
        public bool ReadyForImport => OutputCheck.Succeeded;
        /// <summary>
        /// Processing hasn't started
        /// </summary>
        public bool ReadyToRun => !Processing.Started;

        public void Clear()
        {
            Analyse = new();
            Encode = new();
            Import = new();
            OutputCheck = new();
            Prepare = new();
            Processing = new();
        }
    }
}
