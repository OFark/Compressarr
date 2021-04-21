using Compressarr.FFmpegFactory;
using Compressarr.Filtering;
using Compressarr.JobProcessing.Models;
using Compressarr.Services;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public class JobManager : IJobManager
    {
        private const string jobsFile = "jobs.json";
        private readonly IFFmpegManager fFmpegManager;
        private readonly IFilterManager filterManager;
        private readonly ILogger<JobManager> logger;
        private readonly IProcessManager processManager;
        private readonly IRadarrService radarrService;
        private readonly ISettingsManager settingsManager;
        private readonly ISonarrService sonarrService;
        public JobManager(ILogger<JobManager> logger, ISettingsManager settingsManager, IRadarrService radarrService, ISonarrService sonarrService, IFilterManager filterManager, IFFmpegManager fFmpegManager, IProcessManager processManager)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;
            this.radarrService = radarrService;
            this.sonarrService = sonarrService;
            this.filterManager = filterManager;
            this.fFmpegManager = fFmpegManager;
            this.processManager = processManager;
        }

        public HashSet<Job> Jobs { get; set; }
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
                        //job.AutoImport = newJob.AutoImport;
                        //job.BaseFolder = newJob.BaseFolder;
                        //job.DestinationFolder = newJob.DestinationFolder;
                    }
                    else
                    {
                        logger.LogDebug($"Adding Job ({newJob.Name}).");
                        Jobs.Add(newJob);
                    }

                    await SaveJobs();
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
                    processManager.Stop(job);
                    logger.LogDebug($"Job Process Stopped.");
                }

                UpdateJobState(job, JobState.Finished);
            }
        }

        public async Task DeleteJob(Job job)
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
                await SaveJobs();
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

                    var getMoviesResult = await radarrService.GetMoviesFiltered(filter, filterVals);

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

        public async void Init()
        {
            using (logger.BeginScope("JobManager Initialisation"))
            {
                logger.LogInformation("JobManager Initialising");

                Jobs = await LoadJobs();

                foreach (var job in Jobs)
                {
                    _ = InitialiseJob(job);
                }
            }
        }
        public async Task InitialiseJob(Job job, bool force = false)
        {
            using (logger.BeginScope("Initialise Job"))
            {
                logger.LogInformation($"Job name: {job.Name}");

                Log(job, LogLevel.Information, "Begin Initialisation");

                if ((job.JobState == JobState.New) || force)
                {
                    logger.LogDebug($"Job will initialise.");
                    UpdateJobState(job, JobState.Initialising);
                    job.Filter = filterManager.GetFilter(job.FilterName);
                    job.Preset = fFmpegManager.GetPreset(job.PresetName);
                    logger.LogDebug($"Job using filter: {job.FilterName} and preset: {job.PresetName}.");

                    if (job.Preset != null)
                    {
                        job.Preset.ContainerExtension = fFmpegManager.ConvertContainerToExtension(job.Preset.Container);
                        logger.LogDebug($"Container Extension set to {job.Preset.ContainerExtension}");
                        UpdateJobState(job, JobState.Added);
                        job.Cancel = false;

                        Log(job, LogLevel.Debug, "Begin Testing");

                        if (job.Filter.MediaSource == Filtering.MediaSource.Radarr)
                        {
                            Log(job, LogLevel.Debug, "Job is for Movies, Connecting to Radarr");
                            var radarrURL = settingsManager.GetSetting(SettingType.RadarrURL);
                            var radarrAPIKey = settingsManager.GetSetting(SettingType.RadarrAPIKey);

                            var systemStatus = radarrService.TestConnection(radarrURL, radarrAPIKey);

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
                                File.WriteAllText(testFilePath, "This is a write test");
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
        public async Task<Job> ReloadJob(Job job)
        {
            using (logger.BeginScope("Reload Job"))
            {
                var fileJobs = await LoadJobs();

                logger.LogInformation($"Job ID: {job.ID}");

                var fileJob = fileJobs.FirstOrDefault(j => j.ID == job.ID);

                if (fileJob != null)
                {
                    job.AutoImport = fileJob.AutoImport;
                    job.BaseFolder = fileJob.BaseFolder;
                    job.DestinationFolder = fileJob.DestinationFolder;
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
            using (logger.BeginScope("Run Job"))
            {

                if (job.JobState == JobState.TestedOK || job.JobState == JobState.Waiting || job.JobState == JobState.Finished)
                {
                    UpdateJobState(job, JobState.Running);
                    Log(job, LogLevel.Information, $"Started Running at: {DateTime.Now}");

                    try
                    {
                        foreach (var wi in job.WorkLoad)
                        {
                            if (!job.Cancel)
                            {
                                wi.Success = false;
                                //WorkItem Duration is the current process time frame, MediaInfo Duration is the movie length.
                                var mediaInfo = await fFmpegManager.GetMediaInfo(wi.SourceFile);
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
                                    var checkResult = await fFmpegManager.CheckResult(wi);
                                    if (!checkResult)
                                    {
                                        job.Log("Duration mis-match", LogLevel.Warning);
                                    }
                                    
                                    if(job.MinSSIM > 0 &&  job.MinSSIM > wi.SSIM)
                                    {
                                        job.Log($"SSIM too low: {wi.SSIM}", LogLevel.Warning);
                                        checkResult = false;
                                    }
                                    else
                                    {
                                        job.Log($"SSIM check passed: {wi.SSIM}", LogLevel.Information);
                                    }
                                    
                                    wi.Success = wi.Success && checkResult;
                                }

                            }
                        }
                    }
                    finally
                    {
                        UpdateJobState(job, JobState.Finished);
                    }
                }
            }
        }

        private void Fail(Job job)
        {
            UpdateJobState(job, JobState.TestedFail);
            job.Log("Test failed", LogLevel.Warning);
        }

        private async Task<HashSet<Job>> LoadJobs()
        {
            using (logger.BeginScope("Load Jobs"))
            {
                return await settingsManager.LoadSettingFile<HashSet<Job>>(jobsFile) ?? new();
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

        private async Task SaveJobs()
        {
            using (logger.BeginScope("Save Jobs"))
            {
                await settingsManager.SaveSettingFile(jobsFile, Jobs);
            }
        }

        private void Succeed(Job job)
        {
            job.ID ??= Guid.NewGuid();

            UpdateJobState(job, JobState.TestedOK);
            job.Log("Test succeeded", LogLevel.Information);
        }
        private void UpdateJobState(Job job, JobState state)
        {
            logger.LogInformation($"Job {job.Name} changed from {job.JobState} to {state}.");
            job.UpdateState(state);
        }
    }
}