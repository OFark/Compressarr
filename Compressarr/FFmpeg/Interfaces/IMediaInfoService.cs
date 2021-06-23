using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Compressarr.Services.Interfaces;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public interface IMediaInfoService
    {
        Task<FFProbeResponse> GetMediaInfo(IMedia media);
    }
}