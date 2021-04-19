using Compressarr.FFmpegFactory;
using Compressarr.Filtering;
using Compressarr.JobProcessing.Models;
using Compressarr.Services;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.AspNetCore.Hosting;
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
        private string jobsFilePath => Path.Combine(_env.ContentRootPath, "config", "jobs.json");

        public HashSet<Job> Jobs { get; set; }

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<JobManager> logger;
        internal IRadarrService radarrService;
        internal ISonarrService sonarrService;
        internal IFilterManager filterManager;
        internal ISettingsManager settingsManager;
        internal IFFmpegManager fFmpegManager;
        internal IProcessManager processManager;

        public JobManager(IWebHostEnvironment env, ILogger<JobManager> logger, ISettingsManager settingsManager, IRadarrService radarrService, ISonarrService sonarrService, IFilterManager filterManager, IFFmpegManager fFmpegManager, IProcessManager processManager)
        {
            _env = env;
            this.logger = logger;
            this.settingsManager = settingsManager;
            this.radarrService = radarrService;
            this.sonarrService = sonarrService;
            this.filterManager = filterManager;
            this.fFmpegManager = fFmpegManager;
            this.processManager = processManager;
        }

        public async void Init()
        {
            logger.LogInformation($"JobManager Initialising.");

            Jobs = await LoadJobs();

            foreach (var job in Jobs)
            {
                _ = InitialiseJob(job);
            }
        }

        public async Task AddJob(Job newJob)
        {

            var job = Jobs.FirstOrDefault(j => j.Name == newJob.Name);

            if (job != null)
            {
                logger.LogDebug($"Updating Existing Job.");
                job.AutoImport = newJob.AutoImport;
                job.BaseFolder = newJob.BaseFolder;
                job.DestinationFolder = newJob.DestinationFolder;
            }
            else
            {
                logger.LogDebug($"Adding Job ({newJob.Name}).");
                job = newJob;
                Jobs.Add(newJob);
            }

            UpdateJobState(job, JobState.Added);
            await SaveJobs();
            await InitialiseJob(job, true);
        }

        public void CancelJob(Job job)
        {
            logger.LogDebug($"Cancelling Job ({job.Name}).");

            job.Cancel = true;
            if (job.Process != null)
            {
                logger.LogDebug($"Job Process needs stopping.");
                processManager.Stop(job);
                logger.LogDebug($"Job Process Stopped.");
            }

            UpdateJobState(job, JobState.Finished);
        }

        public async Task DeleteJob(Job job)
        {
            logger.LogDebug($"Delete Job ({job.Name}).");

            job = Jobs.FirstOrDefault(j => j.Name == job.Name);

            if (job != null)
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

        public async Task InitialiseJob(Job job, bool force = false)
        {
            logger.LogDebug($"Initialise Job ({job.Name}).");
            Log(job, LogLevel.Information, "Begin Initialisation");

            if ((job.JobState == JobState.New) || force)
            {
                logger.LogDebug($"Job Initialising.");
                UpdateJobState(job, JobState.Initialising);
                job.Filter = filterManager.GetFilter(job.FilterName);
                job.Preset = fFmpegManager.GetPreset(job.PresetName);
                logger.LogDebug($"Job using filter: {job.FilterName} and preset: {job.PresetName}.");

                if (job.Preset != null)
                {
                    job.Preset.ContainerExtension = fFmpegManager.ConvertContainerToExtension(job.Preset.Container);
                    logger.LogDebug($"Containter Extension set to {job.Preset.ContainerExtension}");
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
                            wi.Arguments = job.Preset.GetArgumentString();
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

        public async Task<ServiceResult<HashSet<WorkItem>>> GetFiles(Job job)
        {
            if (job.Filter.MediaSource == MediaSource.Radarr)
            {
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

            return new ServiceResult<HashSet<WorkItem>>(false, "404", "Not Implemented");
        }

        public async void RunJob(Job job)
        {
            logger.LogDebug($"Run Job.");

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

        private void Fail(Job job)
        {
            UpdateJobState(job, JobState.TestedFail);
            job.Log("Test failed", LogLevel.Warning);
        }

        private void Succeed(Job job)
        {
            UpdateJobState(job, JobState.TestedOK);
            job.Log("Test succeeded", LogLevel.Information);
        }

        private async Task SaveJobs()
        {
            logger.LogDebug($"Saving jobs to {jobsFilePath}.");

            var json = JsonConvert.SerializeObject(Jobs, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(jobsFilePath)))
            {
                logger.LogDebug($"Creating missing directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(jobsFilePath));
            }

            await File.WriteAllTextAsync(jobsFilePath, json);
        }

        private async Task<HashSet<Job>> LoadJobs()
        {
            logger.LogDebug($"Loading jobs from {jobsFilePath}.");

            if (File.Exists(jobsFilePath))
            {
                var json = await File.ReadAllTextAsync(jobsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonConvert.DeserializeObject<HashSet<Job>>(json);
                }
            }
            else
            {
                logger.LogDebug($"Jobs file does not exist.");
            }

            return new();
        }

        private void Log(Job job, LogLevel level, params string[] messages)
        {
            foreach (var m in messages.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                logger.Log(level, $"Job {job.Name}: {m}.", null);
                job.Log(m, level);
            }
        }

        private void UpdateJobState(Job job, JobState state)
        {
            logger.LogInformation($"Job {job.Name} changed from {job.JobState} to {state}.");
            job.UpdateState(state);
        }
    }
}