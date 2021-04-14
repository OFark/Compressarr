namespace Compressarr.JobProcessing
{
    public enum JobStatus
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