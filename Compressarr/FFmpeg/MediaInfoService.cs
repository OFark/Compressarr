using Compressarr.Application;
using Compressarr.FFmpeg.Models;
using Compressarr.Filtering;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.FFmpeg
{
    public class MediaInfoService : IMediaInfoService
    {

        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly ILogger<MediaInfoService> logger;

        private SemaphoreSlim mediaInfoSemaphore = new(1, 1);

        public MediaInfoService(IApplicationService applicationService, IFFmpegProcessor fFmpegProcessor, IFileService fileService, ILogger<MediaInfoService> logger)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.logger = logger;
        }

        public async Task<FFProbeResponse> GetMediaInfo(MediaSource source, string filePath)
        {
            if (source == MediaSource.Radarr)
            {
                var movie = applicationService.Movies.FirstOrDefault(m => filePath == m.GetFullPath(applicationService.RadarrSettings.BasePath));

                if (movie != null)
                {
                    if (movie.MediaInfo != null)
                    {
                        return movie.MediaInfo;
                    }

                    var cacheHash = movie.GetHashCode();

                    using (logger.BeginScope($"Getting Cached MediaInfo: {filePath}", filePath))
                    {
                        if (cacheHash != 0 && applicationService.CacheMediaInfo)
                        {
                            if (fileService.HasFile(AppDir.Cache, $"{cacheHash}.json"))
                            {
                                var cachedInfo = await fileService.ReadJsonFileAsync<FFProbeResponse>(AppDir.Cache, $"{cacheHash}.json");
                                if (cachedInfo != null)
                                {
                                    logger.LogInformation($"Returning Info from cache");
                                    return movie.MediaInfo = cachedInfo;
                                }
                            }
                        }
                    }
                }


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
                            var ffProbeInfo = ffProbeResponse.Result;
                            if (movie != null)
                            {
                                movie.MediaInfo = ffProbeInfo;
                                if (applicationService.CacheMediaInfo)
                                {
                                    await fileService.WriteJsonFileAsync(AppDir.Cache, $"{movie.GetHashCode()}.json", ffProbeInfo);
                                }
                            }
                            return ffProbeInfo;
                        }
                    }
                }
                finally
                {
                    mediaInfoSemaphore.Release();
                }
            }
            else if (source == MediaSource.Sonarr)
            {

            }

            return null;

        }
    }
}

