using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Compressarr.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public interface IMediaInfoService
    {
        Task<FFResult<FFProbeResponse>> GetMediaInfo(IMedia media, CancellationToken token);
    }
}