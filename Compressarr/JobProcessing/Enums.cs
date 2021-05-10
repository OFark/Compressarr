namespace Compressarr.JobProcessing
{
    public enum JobState
    {
        New,
        Initialising,
        Testing,
        TestedFail,
        TestedOK,
        Running,
        Waiting,
        Finished
    }
}