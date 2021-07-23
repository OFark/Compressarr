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
        Uninitialised,
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