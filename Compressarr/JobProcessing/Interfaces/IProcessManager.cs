using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing.Models;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public interface IProcessManager
    {
        Task Process(Job job);
    }
}