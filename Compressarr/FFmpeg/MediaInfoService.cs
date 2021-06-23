using Compressarr.Application;
using Compressarr.FFmpeg.Models;
using Compressarr.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public class MediaInfoService : IMediaInfoService
    {

        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly ILogger<MediaInfoService> logger;

        private readonly SemaphoreSlim mediaInfoSemaphore = new(1, 1);

        public MediaInfoService(IApplicationService applicationService, IFFmpegProcessor fFmpegProcessor, ILogger<MediaInfoService> logger)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.logger = logger;
        }

        public async Task<FFProbeResponse> GetMediaInfo(IMedia media)
        {
            var filePath = media.FilePath;

            await mediaInfoSemaphore.WaitAsync();
            try
            {
                using (logger.BeginScope($"Getting Source MediaInfo: {filePath}", filePath))
                {
                    //Wait for FFmpeg to be ready
                    await applicationService.InitialiseFFmpeg;

                    logger.LogInformation($"Loading Info from source");
                    var ffProbeResponse = await fFmpegProcessor.GetFFProbeInfo(filePath);

                    if (ffProbeResponse.Success)
                    {
                        return media.MediaInfo = ffProbeResponse.Result;
                    }
                }
            }
            finally
            {
                mediaInfoSemaphore.Release();
            }
            return null;
        }

    }
}

