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
            LengthOK ? SSIMOK ? SizeOK ? "Passed Checks" : $"File size above threshold ({Math.Round((workItem.Compression ?? 0) * 100, 2)}%)" : $"File similarity below threshold ({Math.Round((workItem.SSIM ?? 0) * 100, 2)}%)" : "File length mismatch";
    }
}
