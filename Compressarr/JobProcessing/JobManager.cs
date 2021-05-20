using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Services;
using Compressarr.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public class JobManager : IJobManager
    {
        private readonly IApplicationService applicationService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly IFilterManager filterManager;
        private readonly IOptionsMonitor<HashSet<Job>> jobsMonitor;
        private readonly ILogger<JobManager> logger;
        private readonly IMediaInfoService mediaInfoService;
        private readonly IPresetManager presetManager;
        private readonly IProcessManager processManager;
        private readonly IRadarrService radarrService;
        private readonly ISonarrService sonarrService;


        public JobManager(IApplicationService applicationService, IFFmpegProcessor fFmpegProcessor, IFileService fileService, IFilterManager filterManager, ILogger<JobManager> logger, IMediaInfoService mediaInfoService, IOptionsMonitor<HashSet<Job>> jobsMonitor, IPresetManager presetManager, IProcessManager processManager, IRadarrService radarrService, ISonarrService sonarrService)
        {
            this.applicationService = applicationService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.filterManager = filterManager;
            this.jobsMonitor = jobsMonitor;
            this.logger = logger;
            this.mediaInfoService = mediaInfoService;
            this.presetManager = presetManager;
            this.processManager = processManager;
            this.radarrService = radarrService;
            this.sonarrService = sonarrService;
        }

        public HashSet<Job> Jobs => applicationService.Jobs;

        public async Task<bool> AddJobAsync(Job newJob)
        {
            using (logger.BeginScope("Adding Job"))
            {
                logger.LogInformation($"Job name: {newJob.Name}");

                await InitialiseJob(newJob);

                if (newJob.Condition.Test.State == ConditionState.Succeeded)
                {
                    logger.LogInformation("Job is OK");

                    if (Jobs.Contains(newJob))
                    {
                        logger.LogDebug($"Updating Existing Job.");
                    }
                    else
                    {
                        logger.LogDebug($"Adding Job ({newJob.Name}).");
                        Jobs.Add(newJob);
                    }

                    _ = applicationService.SaveAppSetting();
                    return true;
                }
                return false;
            }
        }

        public void CancelJob(Job job)
        {
            using (logger.BeginScope("Cancel Log"))
            {
                logger.LogInformation($"Job name: {job.Name}");

                job.Cancel = true;
                if (job.Process != null)
                {
                    logger.LogDebug($"Job Process needs stopping.");
                    Stop(job);
                    logger.LogDebug($"Job Process Stopped.");
                }
            }
        }

        private async Task<WorkItemCheckResult> CheckResult(Job job)
        {

            using (logger.BeginScope("Checking Results."))
            {
                if (job?.Process?.WorkItem == null)
                {
                    return null;
                }
                var result = new WorkItemCheckResult(job.Process.WorkItem);

                var ffProbeResponse = await fFmpegProcessor.GetFFProbeInfo(job.Process.WorkItem.DestinationFile);
                if (ffProbeResponse.Success)
                {
                    var mediaInfo = ffProbeResponse.Result;
                    //Workitem.Duration refers to the processing time frame.
                    logger.LogDebug($"Original Duration: {job.Process.WorkItem.TotalLength}");
                    logger.LogDebug($"New Duration: {mediaInfo?.format?.Duration}");

                    result.LengthOK = mediaInfo?.format?.Duration != default && job.Process.WorkItem.TotalLength.HasValue &&
                        (long)Math.Round(mediaInfo.format.Duration.TotalSeconds, 0) == (long)Math.Round(job.Process.WorkItem.TotalLength.Value.TotalSeconds, 0); //Check to the nearest second.
                }

                result.SSIMOK = result.LengthOK &&
                    (!job.SSIMCheck || job.MinSSIM <= job.Process.WorkItem.SSIM);


                result.SizeOK = result.SSIMOK &&
                    (!job.SizeCheck || job.MaxCompression >= job.Process.WorkItem.Compression);

                return result;
            }
        }
        public Task DeleteJob(Job job)
        {
            using (logger.BeginScope("Delete Job"))
            {
                logger.LogInformation($"Job name: {job.Name}");

                if (Jobs.Contains(job))
                {
                    Jobs.Remove(job);
                }
                else
                {
                    logger.LogWarning($"Job not found.");
                }

                job = null;
                return applicationService.SaveAppSetting();
            }
        }

        public bool FilterInUse(string filterName)
        {
            return Jobs.Any(j => j.FilterName == filterName);
        }

        public async Task InitialiseJob(Job job)
        {
            using (logger.BeginScope("Initialise Job: {job}", job))
            {
                logger.LogInformation($"Job name: {job.Name}");

                job.LogAction = (level, message) =>
                {
                    logger.Log(level, message);
                };

                if (job.Condition.SafeToInitialise)
                {
                    Log(job, LogLevel.Information, "Begin Initialisation");
                    job.UpdateCondition((c) => c.Clear());

                    logger.LogDebug($"Job will initialise.");
                    using (var jobInit = new JobWorker(job.Condition.Initialise))
                    {
                        job.Filter = filterManager.GetFilter(job.FilterName);
                        job.Preset = presetManager.GetPreset(job.PresetName);
                        logger.LogDebug($"Job using filter: {job.FilterName} and preset: {job.PresetName}.");

                        if (job.Preset != null)
                        {
                            job.Preset.ContainerExtension = await presetManager.ConvertContainerToExtension(job.Preset.Container);
                            logger.LogDebug($"Container Extension set to {job.Preset.ContainerExtension}");
                            using (var jobTest = new JobWorker(job.Condition.Test))
                            {

                                job.Cancel = false;

                                Log(job, LogLevel.Debug, "Begin Testing");

                                if (job.Filter.MediaSource == MediaSource.Radarr)
                                {
                                    Log(job, LogLevel.Debug, "Job is for Movies, Connecting to Radarr");

                                    var systemStatus = await radarrService.TestConnection(applicationService.RadarrSettings);

                                    if (!systemStatus.Success)
                                    {
                                        Log(job, LogLevel.Warning, "Failed to connect to Radarr.");
                                        Fail(job);
                                        return;
                                    }

                                    Log(job, LogLevel.Debug, "Connected to Radarr", "Fetching List of files from Radarr");

                                    var getFilesResults = await GetFiles(job);

                                    if (!getFilesResults.Success)
                                    {
                                        Log(job, LogLevel.Warning, "Failed to list files from Radarr.");
                                        Fail(job);
                                        return;
                                    }

                                    Log(job, LogLevel.Debug, "Files Returned", "Building Workload");

                                    using (var jobWorkLoad = new JobWorker(job.Condition.BuildWorkLoad))
                                    {

                                        double i = 0;
                                        job.WorkLoad = getFilesResults.Results.ToHashSet();
                                        foreach (var wi in job.WorkLoad)
                                        {
                                            var file = new FileInfo(wi.SourceFile);
                                            if (!file.Exists)
                                            {
                                                Log(job, LogLevel.Warning, $"This file was not found: {file.FullName}");
                                                Fail(job);
                                                return;
                                            }

                                            var destinationpath = job.DestinationFolder;

                                            if (string.IsNullOrWhiteSpace(destinationpath))
                                            {
                                                destinationpath = file.Directory.FullName;
                                            }
                                            else
                                            {
                                                destinationpath = Path.Combine(destinationpath, file.Directory.Name);
                                            }

                                            if (!Directory.Exists(destinationpath))
                                            {
                                                try
                                                {
                                                    Directory.CreateDirectory(destinationpath);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log(job, LogLevel.Error, ex.Message);
                                                    Fail(job);
                                                    return;
                                                }
                                            }

                                            wi.DestinationFile = Path.Combine(destinationpath, file.Name);
                                            if (!string.IsNullOrWhiteSpace(job.Preset.ContainerExtension))
                                            {
                                                wi.DestinationFile = Path.ChangeExtension(wi.DestinationFile, job.Preset.ContainerExtension);
                                            }

                                            job.InitialisationProgress?.Report(++i / job.WorkLoad.Count() * 100);
                                            job.UpdateStatus(this);
                                        }
                                        jobWorkLoad.Succeed();
                                        Log(job, LogLevel.Debug, "Workload complied", "Checking Destination Folder", "Writing Test.txt file");
                                    }

                                    var testFilePath = Path.Combine(job.DestinationFolder, "Test.txt");

                                    try
                                    {
                                        await fileService.WriteTextFileAsync(testFilePath, "This is a write test");

                                        fileService.DeleteFile(testFilePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log(job, LogLevel.Error, ex.Message);
                                        Fail(job);
                                        return;
                                    }

                                    // Success - Job should be good.
                                    jobTest.Succeed();
                                    job.Log("Test succeeded", LogLevel.Information);

                                    job.ID ??= Guid.NewGuid();
                                    jobInit.Succeed();
                                    job.Log("Initialisation succeeded", LogLevel.Information);

                                    return;
                                }
                            }
                        }
                        else
                        {
                            Log(job, LogLevel.Debug, $"Preset {job.PresetName} does not exist");
                        }

                        Fail(job);
                    }
                }
            }
        }

        public void InitialiseJobs(Filter filter)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.Filter == filter))
            {
                _ = InitialiseJob(job);
            }
        }

        public void InitialiseJobs(MediaSource source)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.Filter.MediaSource == source))
            {
                _ = InitialiseJob(job);
            }
        }

        public void InitialiseJobs(FFmpegPreset preset)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.Preset == preset))
            {
                _ = InitialiseJob(job);
            }
        }

        public bool PresetInUse(FFmpegPreset preset)
        {
            return Jobs.Any(j => j.Preset == preset);
        }

        public Job ReloadJob(Job job)
        {
            using (logger.BeginScope("Reload Job"))
            {
                logger.LogInformation($"Job ID: {job.ID}");

                var fileJobs = jobsMonitor?.CurrentValue;

                var fileJob = fileJobs?.FirstOrDefault(j => j.ID == job.ID);

                if (fileJob != null)
                {
                    job = fileJob.JsonClone();
                }
                else
                {
                    logger.LogWarning("Job not found");
                }

                _ = InitialiseJob(job);

                return job;
            }
        }

        public async void RunJob(Job job)
        {
            using (logger.BeginScope("Run Job: {job}", job))
            {
                if (job.Condition.SafeToRun)
                {
                    using (var jobCompleter = new JobWorker(job.Condition.Process))
                    {
                        Log(job, LogLevel.Information, $"Started Job at: {DateTime.Now}");

                        try
                        {
                            foreach (var wi in job.WorkLoad)
                            {
                                if (!job.Cancel)
                                {
                                    wi.Success = false;

                                    var mediaInfo = await mediaInfoService.GetMediaInfo(wi.Source, wi.SourceFile);

                                    wi.TotalLength = mediaInfo?.format?.Duration;
                                    wi.Arguments ??= await presetManager.GetArguments(job.Preset, mediaInfo);

                                    Log(job, LogLevel.Debug, $"Now Processing: {wi.SourceFileName}");
                                    job.Process = new FFmpegProcess();
                                    job.Process.OnUpdate += job.UpdateStatus;
                                    job.Process.WorkItem = wi;
                                    await processManager.Process(job);

                                    if (!job.Cancel) //Job.Cancel is on at this point if the job was cancelled.
                                    {
                                        var checkResult = await CheckResult(job);
                                        if (checkResult != null)
                                        {
                                            if (checkResult.AllGood)
                                            {
                                                job.Log(checkResult.Result, LogLevel.Debug);
                                            }
                                            else
                                            {
                                                job.Log(checkResult.Result, LogLevel.Warning);
                                            }
                                        }
                                        else
                                        {
                                            job.Log("Cannot complete checks, Workitem or Process missing", LogLevel.Error);
                                        }

                                        wi.Success = wi.Success && checkResult.AllGood;

                                        if (wi.Success && job.AutoImport)
                                        {
                                            switch (job.Filter.MediaSource)
                                            {
                                                case MediaSource.Radarr:
                                                    {
                                                        job.Log("Auto Import - Importing into Radarr", LogLevel.Information);
                                                        var response = await radarrService.ImportMovie(wi);
                                                        if (response.Success)
                                                        {
                                                            job.Log("Movie Imported", LogLevel.Information);
                                                        }
                                                        else
                                                        {
                                                            job.Log($"Import Failed [{response.ErrorCode}]: {response.ErrorMessage}", LogLevel.Warning);
                                                        }
                                                    }
                                                    break;
                                            }
                                        }

                                    }

                                }
                            }

                            jobCompleter.Succeed(!job.Cancel);
                        }
                        catch (Exception ex)
                        {
                            Log(job, LogLevel.Error, ex.Message);
                            return;
                        }
                    }
                }
            }
        }

        public void Stop(Job job)
        {
            using (logger.BeginScope("Stop Processing"))
            {
                if (job.Process != null)
                {
                    job.Process.cont = false;
                    job.Log("Job Stop requested", LogLevel.Information);
                    if (job.Process.Converter != null)
                    {
                        logger.LogDebug("Cancellation Token Set");
                        job.Process.cancellationTokenSource.Cancel();
                    }
                }
                else
                {
                    logger.LogWarning("Job process cannot be stopped, Process is null");
                }
            }
        }

        private void Fail(Job job)
        {
            job.Log("Test failed", LogLevel.Warning);
        }

        private async Task<ServiceResult<HashSet<WorkItem>>> GetFiles(Job job)
        {
            using (logger.BeginScope("Get Files"))
            {
                if (job.Filter.MediaSource == MediaSource.Radarr)
                {
                    logger.LogInformation("From Radarr");

                    var filter = filterManager.ConstructFilterQuery(job.Filter.Filters, out var filterVals);

                    var getMoviesResponse = await radarrService.GetMoviesFilteredAsync(filter, filterVals);

                    if (!getMoviesResponse.Success)
                    {
                        return new(false, getMoviesResponse.ErrorCode, getMoviesResponse.ErrorMessage);
                    }

                    var movies = getMoviesResponse.Results;
                    return new(true, movies.Select(x => new WorkItem(x, applicationService.RadarrSettings.BasePath)).ToHashSet());
                }

                logger.LogWarning($"Source ({job.Filter.MediaSource}) is not supported");
                return new(false, "404", "Not Implemented");
            }
        }
        private void Log(Job job, LogLevel level, params string[] messages)
        {
            foreach (var m in messages.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                logger.Log(level, $"Job {job.Name}: {m}.", null);
                job.Log(m, level);
            }
        }
    }
}