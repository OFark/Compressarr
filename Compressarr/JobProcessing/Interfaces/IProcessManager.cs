using Compressarr.FFmpeg.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Models;
using System;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IProcessManager
    {
        Task Process(Job job);
    }
}