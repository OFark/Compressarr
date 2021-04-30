using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Services;
using Compressarr.Services.Models;
using Compressarr.Settings;
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
        private readonly IFFmpegManager fFmpegManager;
        private readonly IFilterManager filterManager;
        private readonly ILogger<JobManager> logger;
        private readonly IOptionsMonitor<HashSet<Job>> jobsSnapshot;
        private readonly IProcessManager processManager;
        private readonly IRadarrService radarrService;
        private readonly ISettingsManager settingsManager;
        private readonly ISonarrService sonarrService;


        public JobManager(IFFmpegManager fFmpegManager, IFilterManager filterManager, ISettingsManager settingsManager, ILogger<JobManager> logger, IOptionsMonitor<HashSet<Job>> jobsMonitor, IProcessManager processManager, IRadarrService radarrService, ISonarrService sonarrService)
        {
            this.fFmpegManager = fFmpegManager;
            this.filterManager = filterManager;
            this.jobsSnapshot = jobsMonitor;
            this.logger = logger;
            this.processManager = processManager;
            this.radarrService = radarrService;
            this.settingsManager = settingsManager;
            this.sonarrService = sonarrService;
        }

        public HashSet<Job> Jobs => settingsManager.Jobs;
        
        public async Task<bool> AddJob(Job newJob)
        {
            using (logger.BeginScope("Adding Job"))
            {
                logger.LogInformation($"Job name: {newJob.Name}");

                await InitialiseJob(newJob, true);

                if (newJob.JobState == JobState.TestedOK)
                {
                    logger.LogInformation("Job is OK");

                    if (Jobs.Contains(newJob))
                    {
                        logger.LogDebug($"Updating Existing Job.");
                    }
                    else
                    {
                        logger.LogDebug($"Adding Job ({newJob.Name}).");
                        var job = newJob.Clone();
                        _ = InitialiseJob(job);
                        Jobs.Add(job);
                    }

                    _ = settingsManager.SaveAppSetting();
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

                job.UpdateState(JobState.Finished);
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
                return settingsManager.SaveAppSetting();
            }
        }

        public async Task<ServiceResult<HashSet<WorkItem>>> GetFiles(Job job)
        {
            using (logger.BeginScope("Get Files"))
            {
                if (job.Filter.MediaSource == MediaSource.Radarr)
                {
                    logger.LogInformation("From Radarr");

                    var filter = filterManager.ConstructFilterQuery(job.Filter.Filters, out var filterVals);

                    var getMoviesResult = await radarrService.GetMoviesFilteredAsync(filter, filterVals);

                    if (!getMoviesResult.Success)
                    {
                        return new ServiceResult<HashSet<WorkItem>>(false, getMoviesResult.ErrorCode, getMoviesResult.ErrorMessage);
                    }

                    var movies = getMoviesResult.Results;
                    var paths = movies.Select(x => new { x.id, Path = $"{job.BaseFolder}{Path.Combine(x.path, x.movieFile.relativePath)}" }).ToList();

                    return new ServiceResult<HashSet<WorkItem>>(true, paths.Select(p => new WorkItem() { Source = job.Filter.MediaSource, SourceID = p.id, SourceFile = p.Path }).ToHashSet());
                }

                logger.LogWarning($"Source ({job.Filter.MediaSource}) is not supported");
                return new ServiceResult<HashSet<WorkItem>>(false, "404", "Not Implemented");
            }
        }

        public async Task InitialiseJob(Job job, bool force = false)
        {
            using (logger.BeginScope("Initialise Job: {job}", job))
            {
                logger.LogInformation($"Job name: {job.Name}");

                job.LogAction = (level, message) =>
                {
                    logger.Log(level, message);
                };

                Log(job, LogLevel.Information, "Begin Initialisation");

                if ((job.JobState == JobState.New) || force)
                {
                    logger.LogDebug($"Job will initialise.");
                    job.UpdateState(JobState.Initialising);
                    job.Filter = filterManager.GetFilter(job.FilterName);
                    job.Preset = fFmpegManager.GetPreset(job.PresetName);
                    logger.LogDebug($"Job using filter: {job.FilterName} and preset: {job.PresetName}.");

                    if (job.Preset != null)
                    {
                        job.Preset.ContainerExtension = fFmpegManager.ConvertContainerToExtension(job.Preset.Container);
                        logger.LogDebug($"Container Extension set to {job.Preset.ContainerExtension}");
                        job.UpdateState(JobState.Added);
                        job.Cancel = false;

                        Log(job, LogLevel.Debug, "Begin Testing");

                        if (job.Filter.MediaSource == Filtering.MediaSource.Radarr)
                        {
                            Log(job, LogLevel.Debug, "Job is for Movies, Connecting to Radarr");
                            //var radarrURL = settingsManager.Settings[SettingType.RadarrURL];
                            //var radarrAPIKey =settingsManager.Settings[SettingType.RadarrAPIKey];

                            var systemStatus = await radarrService.TestConnection(settingsManager.RadarrSettings);

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

                            job.WorkLoad = getFilesResults.Results;
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

                                wi.DestinationFile = Path.ChangeExtension(Path.Combine(destinationpath, file.Name), job.Preset.ContainerExtension);
                            }

                            Log(job, LogLevel.Debug, "Workload complied", "Checking Destination Folder", "Writing Test.txt file");

                            var testFilePath = Path.Combine(job.WriteFolder, "Test.txt");

                            try
                            {
                                await settingsManager.WriteTextFileAsync(testFilePath, "This is a write test");

                                //todo FileManager this 
                                File.Delete(testFilePath);
                            }
                            catch (Exception ex)
                            {
                                Log(job, LogLevel.Error, ex.Message);
                                Fail(job);
                                return;
                            }

                            Succeed(job);
                            return;
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
        public Job ReloadJob(Job job)
        {
            using (logger.BeginScope("Reload Job"))
            {
                logger.LogInformation($"Job ID: {job.ID}");

                var fileJobs = jobsSnapshot?.CurrentValue;

                var fileJob = fileJobs?.FirstOrDefault(j => j.ID == job.ID);

                if (fileJob != null)
                {
                    job = fileJob.Clone();
                }
                else
                {
                    logger.LogWarning("Job not found");
                }

                _ = InitialiseJob(job, true);

                return job;
            }
        }

        public async void RunJob(Job job)
        {
            using (logger.BeginScope("Run Job: {job}", job))
            {

                if (job.JobState == JobState.TestedOK ||  job.JobState == JobState.Finished)
                {
                    job.UpdateState(JobState.Waiting);
                    Log(job, LogLevel.Information, $"Started Job at: {DateTime.Now}");

                    try
                    {
                        foreach (var wi in job.WorkLoad)
                        {
                            if (!job.Cancel)
                            {
                                wi.Success = false;
                                //WorkItem Duration is the current process time frame, MediaInfo Duration is the movie length.
                                var mediaInfo = await fFmpegManager.GetMediaInfoAsync(wi.SourceFile);
                                if (mediaInfo != null)
                                {
                                    wi.TotalLength = TimeSpan.FromSeconds((long)Math.Round(mediaInfo.Duration.TotalSeconds, 0));
                                }

                                Log(job, LogLevel.Debug, $"Now Processing: {wi.SourceFileName}");
                                job.Process = new FFmpegProcess();
                                job.Process.OnUpdate += job.UpdateStatus;
                                job.Process.WorkItem = wi;
                                await processManager.Process(job);

                                if (!job.Cancel) //Job.Cancel is on at this point if the job was cancelled.
                                {
                                    var checkResult = await fFmpegManager.CheckResult(job);
                                    if (checkResult != null)
                                    {
                                        if(checkResult.AllGood)
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

                                    if(wi.Success && job.AutoImport)
                                    {
                                        switch(job.Filter.MediaSource)
                                        {
                                            case MediaSource.Radarr:
                                                {
                                                    job.Log("Auto Import - Importing into Radarr", LogLevel.Information);
                                                    var response = await radarrService.ImportMovie(wi);
                                                    if(response.Success)
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
                    }
                    finally
                    {
                        job.UpdateState(JobState.Finished);
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
            job.UpdateState(JobState.TestedFail);
            job.Log("Test failed", LogLevel.Warning);
        }

        private void Log(Job job, LogLevel level, params string[] messages)
        {
            foreach (var m in messages.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                logger.Log(level, $"Job {job.Name}: {m}.", null);
                job.Log(m, level);
            }
        }

        private void Succeed(Job job)
        {
            job.ID ??= Guid.NewGuid();

            job.UpdateState(JobState.TestedOK);
            job.Log("Test succeeded", LogLevel.Information);
        }
    }
}