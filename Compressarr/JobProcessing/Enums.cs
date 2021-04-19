namespace Compressarr.JobProcessing
{
    public enum JobState
    {
        New,
        Initialising,
        Added,
        TestedFail,
        TestedOK,
        Running,
        Waiting,
        Finished
    }
}