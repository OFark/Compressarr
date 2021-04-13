using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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