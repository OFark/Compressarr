using Compressarr.Application;
using Compressarr.Services.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public class FolderService : IFolderService
    {
        private readonly IApplicationService applicationService;
        private readonly ILogger<FolderService> logger;
        public FolderService(IApplicationService applicationService, ILogger<FolderService> logger)
        {
            this.applicationService = applicationService;
            this.logger = logger;
        }

        public async Task<SystemStatus> TestConnectionAsync(string path)
        {
            using (logger.BeginScope("Test Connection", path))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    logger.LogDebug("Test aborted, due to missing path");
                    return new() { Success = false, ErrorMessage = "Path is missing" };
                }
                logger.LogInformation($"Test Folder Connection.");
                SystemStatus ss = new();

                logger.LogDebug($"Path: {path}");

                return await Task.Run(() =>
                {
                    if (Directory.Exists(path))
                    {
                        var di = new DirectoryInfo(path);
                        if (di.GetFiles("*", SearchOption.AllDirectories).Any())
                        {
                            if (di.GetFiles("*", SearchOption.AllDirectories).Any(x => applicationService.DemuxerExtensions.Contains(x.Extension.ToLower().TrimStart('.'))))
                            {
                                return new SystemStatus() { Success = true, startupPath = path };
                            }
                            else
                            {
                                return new() { Success = false, ErrorMessage = $"Folder ({path}) doesn't contain any files the FFmpeg can demux" };
                            }
                        }
                        else
                        {
                            return new() { Success = false, ErrorMessage = $"Folder ({path}) doesn't contain any files" };
                        }
                    }
                    else
                    {
                        return new() { Success = false, ErrorMessage = $"Folder ({path}) doesn't exist" };
                    }
                });
            }
        }

        public async Task<ServiceResult<IEnumerable<FileInfo>>> RequestFilesAsync(string path)
        {
            using (logger.BeginScope("Get Files", path))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    logger.LogError("Path is required");
                    return new(false, "404", "Path is missing");
                }

                logger.LogDebug($"Path: {path}");

                return await Task.Run(() =>
                {
                    if (Directory.Exists(path))
                    {
                        var di = new DirectoryInfo(path);
                        if (di.GetFiles("*", SearchOption.AllDirectories).Any())
                        {
                            if (di.GetFiles("*", SearchOption.AllDirectories).Any(x => applicationService.DemuxerExtensions.Contains(x.Extension.ToLower().TrimStart('.'))))
                            {
                                return new ServiceResult<IEnumerable<FileInfo>>(true, di.GetFiles("*", SearchOption.AllDirectories).Where(x => applicationService.DemuxerExtensions.Contains(x.Extension.ToLower().TrimStart('.'))));
                            }
                            else
                            {
                                return new(false, "",$"Folder ({path}) doesn't contain any files the FFmpeg can demux");
                            }
                        }
                        else
                        {
                            return new(false, "", $"Folder ({path}) doesn't contain any files");
                        }
                    }
                    else
                    {
                        return new(false, "", $"Folder ({path}) doesn't exist");
                    }
                });
            }
        }
    }
}
