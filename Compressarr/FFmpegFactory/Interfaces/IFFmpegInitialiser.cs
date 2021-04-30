using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory
{
    public interface IFFmpegInitialiser
    {
        bool Ready { get; }
        string Version { get; }

        Task Start();
    }
}