namespace Compressarr.JobProcessing
{
    public enum JobState
    {
        BuildingWorkLoad,
        Cancelled,
        Error,
        Finished,
        Initialising,
        LoadingMediaInfo,
        New,
        Preparing,
        Ready,
        Running,
        TestedFail,
        TestedOK,
        Testing,
        Waiting,
    }

    public enum ConditionState
    {
        NotStarted,
        Processing,
        Succeeded,
        Failed
    }
}