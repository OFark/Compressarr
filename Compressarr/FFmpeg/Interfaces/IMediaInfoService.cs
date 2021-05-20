using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public interface IMediaInfoService
    {
        Task<FFProbeResponse> GetMediaInfo(MediaSource source, string filePath);
    }
}