using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class WorkItemCheckResult
    {
        private WorkItem workItem;

        public WorkItemCheckResult(WorkItem wi)
        {
            workItem = wi;
        }

        public bool LengthOK { get; set; }
        public bool SSIMOK { get; set; }
        public bool SizeOK { get; set; }

        public bool AllGood => LengthOK && SSIMOK && SizeOK;

        public string Result =>
            LengthOK ? SSIMOK ? SizeOK ? "Passed Checks" : $"File size above threshold ({workItem.Compression * 100}%)" : $"File similarity below threshold ({workItem.SSIM * 100}%)" : "File length mismatch";
    }
}
