using Compressarr.FFmpegFactory;
using Compressarr.Filtering;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Interfaces;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public class JobManager
    {
        private string jobsFilePath => Path.Combine(_env.ContentRootPath, "config", "jobs.json");

        private HashSet<Job> _Jobs { get; set; }
        public HashSet<Job> Jobs => _Jobs;

        private IWebHostEnvironment _env;
        internal IRadarrService radarrService;
        internal ISonarrService sonarrService;
        internal FilterManager filterManager;
        internal SettingsManager settingsManager;
        internal FFmpegManager fFmpegManager;

        public JobManager(IWebHostEnvironment env, SettingsManager settingsManager, IRadarrService radarrService, ISonarrService sonarrService, FilterManager filterManager, FFmpegManager fFmpegManager)
        {
            _env = env;
            this.settingsManager = settingsManager;
            this.radarrService = radarrService;
            this.sonarrService = sonarrService;
            this.filterManager = filterManager;
            this.fFmpegManager = fFmpegManager;

            _Jobs = LoadJobs();

            foreach (var job in _Jobs)
            {
                InitialiseJob(job);
            }

        }

        public void AddJob(Job newJob)
        {
            var job = _Jobs.FirstOrDefault(j => j.Name == newJob.Name);

            if (job != null)
            {
                job.AutoImport = newJob.AutoImport;
                job.BaseFolder = newJob.BaseFolder;
                job.DestinationFolder = newJob.DestinationFolder;
            }
            else
            {
                _Jobs.Add(newJob);
            }

            newJob.JobStatus = JobStatus.Added;
            SaveJobs();
            InitialiseJob(job, true);
        }

        public void CancelJob(Job job)
        {
            job.Cancel = true;
            if (job.Process != null)
            {
                job.Process.Stop();
            }

            job.JobStatus = JobStatus.Finished;
        }

        public void CreateJobTester(Job job)
        {
            job.JobTester ??= new JobTester(this);
        }

        public void InitialiseJob(Job job, bool force = false)
        {
            if ((job.JobStatus == JobStatus.New) || force)
            {
                job.JobStatus = JobStatus.Initialising;
                job.Filter = filterManager.GetFilter(job.FilterName);
                job.Preset = fFmpegManager.GetPreset(job.PresetName);
                job.Preset.ContainerExtension = fFmpegManager.ConvertContainerToExtension(job.Preset.Container);
                job.JobStatus = JobStatus.Added;
                job.Cancel = false;

                job.UpdateStatus("Begin Testing");

                if (job.Filter.MediaSource == Filtering.MediaSource.Radarr)
                {
                    job.UpdateStatus("Job is for Movies, Connecting to Radarr");
                    var radarrURL = settingsManager.GetSetting(SettingType.RadarrURL);
                    var radarrAPIKey = settingsManager.GetSetting(SettingType.RadarrAPIKey);

                    var systemStatus = radarrService.TestConnection(radarrURL, radarrAPIKey);

                    if (!systemStatus.Success)
                    {
                        job.UpdateStatus("Failed to connect to Radarr.");
                        Fail(job);
                        return;
                    }

                    job.UpdateStatus("Connected to Radarr", "Fetching List of files from Radarr");

                    GetFiles(job).ContinueWith(t =>
                    {
                        var getFilesResults = t.Result;
                        if (!getFilesResults.Success)
                        {
                            job.UpdateStatus("Failed to list files from Radarr.");
                            Fail(job);
                            return;
                        }

                        job.UpdateStatus("Files Returned", "Building Workload");

                        job.WorkLoad = getFilesResults.Results;
                        foreach (var wi in job.WorkLoad)
                        {
                            var file = new FileInfo(wi.SourceFile);
                            if (!file.Exists)
                            {
                                job.UpdateStatus($"This file was not found: {file.FullName}");
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
                                    job.UpdateStatus(ex.Message);
                                    Fail(job);
                                    return;
                                }
                            }

                            wi.DestinationFile = Path.ChangeExtension(Path.Combine(destinationpath, file.Name), job.Preset.ContainerExtension);
                            wi.Arguments = job.Preset.GetArgumentString();
                        }
                    });

                    job.UpdateStatus("Workload complied", "Checking Destination Folder", "Writing Test.txt file");

                    var testFilePath = Path.Combine(job.WriteFolder, "Test.txt");

                    try
                    {
                        File.WriteAllText(testFilePath, "This is a write test");
                        File.Delete(testFilePath);
                    }
                    catch (Exception ex)
                    {
                        job.UpdateStatus(ex.Message);
                        Fail(job);
                        return;
                    }

                    Succeed(job);
                    return;
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
            if (job.JobStatus == JobStatus.TestedOK || job.JobStatus == JobStatus.Waiting || job.JobStatus == JobStatus.Finished)
            {
                job.JobStatus = JobStatus.Running;
                job.UpdateStatus($"Started Running at: {DateTime.Now}");

                foreach (var wi in job.WorkLoad)
                {
                    if (!job.Cancel)
                    {
                        wi.Success = false;
                        job.UpdateStatus($"Now Processing: {wi.SourceFileName}");
                        job.Process = new FFmpegProcess();
                        job.Process.OnUpdate += job.UpdateStatus;
                        await job.Process.Process(wi);

                        if (!job.Cancel) //Job.Cancel is on at this point if the job was cancelled.
                        {
                            var checkResult = fFmpegManager.CheckResult(wi);
                            if (!checkResult)
                            {
                                wi.UpdateStatus("Duration mis-match");
                                job.UpdateStatus("Duration mis-match");
                            }
                            wi.Success = wi.Success && checkResult;
                        }
                    }
                }

                job.JobStatus = JobStatus.Finished;
                job.UpdateStatus();
            }
        }

        private void Fail(Job job)
        {
            job.JobStatus = JobStatus.TestedFail;
            job.UpdateStatus("Test failed");
        }

        private void Succeed(Job job)
        {
            job.JobStatus = JobStatus.TestedOK;
            job.UpdateStatus("Test succeeded");
        }

        private void SaveJobs()
        {
            var json = JsonConvert.SerializeObject(_Jobs, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(jobsFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jobsFilePath));
            }

            File.WriteAllText(jobsFilePath, json);
        }

        private HashSet<Job> LoadJobs()
        {
            if (File.Exists(jobsFilePath))
            {
                var json = File.ReadAllText(jobsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonConvert.DeserializeObject<HashSet<Job>>(json);
                }
            }

            return null;
        }
    }
}