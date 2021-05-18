using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing.Models
{
    public class ConditionSwitch
    {
        public bool Finished { get; set; }
        public bool Started { get; set; }
        public bool Success { get; set; }

        public ConditionState State
        {
            get
            {
                if(Started)
                {
                    if(Finished)
                    {
                        if (Success)
                            return ConditionState.Succeeded;
                        return ConditionState.Failed;
                    }
                    return ConditionState.Processing;
                }
                return ConditionState.NotStarted;
            }
        }

        /// <summary>
        /// Started but not yet finished;
        /// </summary>
        public bool Processing => State == ConditionState.Processing;
        /// <summary>
        /// Started and Finished, may not have been sucessful
        /// </summary>
        public bool Done => Started && Finished;

        /// <summary>
        /// Started, finished with success
        /// </summary>
        public bool Succeeded => State == ConditionState.Succeeded;

        /// <summary>
        /// Started, finished without success
        /// </summary>
        public bool Failed => State == ConditionState.Failed;

        public void Start()
        {
            Finished = false;
            Started = true;
            Success = false;
        }

        public void Complete(bool OK = true)
        {
            Success = OK;
        }

        public void Finish()
        {
            Finished = true;
        }
    }

    
}
