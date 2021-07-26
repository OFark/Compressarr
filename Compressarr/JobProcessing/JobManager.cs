using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Services;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public class JobManager : IJobManager
    {
        readonly static SemaphoreSlim processSemaphore = new(1, 1);

        private readonly IApplicationService applicationService;
        private readonly IArgumentService argumentService;
        private readonly IFFmpegProcessor fFmpegProcessor;
        private readonly IFileService fileService;
        private readonly IFilterManager filterManager;
        private readonly IFolderService folderService;
        private readonly IHistoryService historyService;
        private readonly IOptionsMonitor<HashSet<Job>> jobsMonitor;
        private readonly ILogger<JobManager> logger;
        private readonly IPresetManager presetManager;
        //private readonly IProcessManager processManager;
        private readonly IRadarrService radarrService;
        private readonly ISonarrService sonarrService;


        public JobManager(IApplicationService applicationService, IArgumentService argumentService, IFFmpegProcessor fFmpegProcessor, IFileService fileService, IFilterManager filterManager, IFolderService folderService, IHistoryService historyService, ILogger<JobManager> logger, IOptionsMonitor<HashSet<Job>> jobsMonitor, IPresetManager presetManager, IRadarrService radarrService, ISonarrService sonarrService)
        {
            this.applicationService = applicationService;
            this.argumentService = argumentService;
            this.fFmpegProcessor = fFmpegProcessor;
            this.fileService = fileService;
            this.filterManager = filterManager;
            this.folderService = folderService;
            this.historyService = historyService;
            this.jobsMonitor = jobsMonitor;
            this.logger = logger;
            this.presetManager = presetManager;
            //this.processManager = processManager;
            this.radarrService = radarrService;
            this.sonarrService = sonarrService;
        }
        public HashSet<Job> Jobs => applicationService.Jobs;

        public async Task<bool> AddJobAsync(Job newJob, CancellationToken token)
        {
            using (logger.BeginScope("Adding Job"))
            {
                logger.LogInformation($"Job name: {newJob.Name}");

                newJob.PresetName = newJob.Preset?.Name ?? newJob.PresetName;

                await InitialiseJob(newJob, token);

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
                        //var job = newJob.JsonClone();
                        Jobs.Add(newJob); // job);
                        //_ = InitialiseJob(job, token);
                    }

                    await applicationService.SaveAppSetting();
                    return true;
                }
                return false;
            }
        }

        public void CancelJob(Job job)
        {
            using (logger.BeginScope($"Cancel Job - Job name: {job.Name}"))
            {
                job.Cancel();
                logger.LogInformation("Cancellation requested");
            }
        }

        public async Task CheckHistory(WorkItem wi)
        {
            var history = await historyService.GetProcessHistoryAsync(wi.Media.UniqueID);

            if (history != null && history.Any())
            {
                var lastEntry = history.Last();
                if ((lastEntry?.Success ?? false) && (lastEntry?.Arguments == wi.Arguments) && File.Exists(wi.DestinationFile))
                {
                    wi.SSIM = lastEntry.SSIM;
                    wi.Compression = lastEntry.Compression;

                    using (var je = new JobWorker(wi.Condition.Encode, wi.Update))
                    {
                        je.Succeed();
                    }

                    using (var ja = new JobWorker(wi.Condition.Analyse, wi.Update))
                    {
                        ja.Succeed();
                    }

                    wi.Update("Item skipped, it was already successfully processed");
                }
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

        public bool FilterInUse(Guid id)
        {
            return Jobs.Any(j => j.ID == id);
        }

        public async Task<string> ImportVideo(WorkItem wi, MediaSource source)
        {
            using var wiImporter = new JobWorker(wi.Condition.Import, wi.Update);

            switch (source)
            {
                case MediaSource.Radarr:
                    {
                        wi.Update(Update.Information("Importing into Radarr"));

                        var response = await radarrService.ImportMovieAsync(wi);
                        if (response.Success)
                        {
                            wiImporter.Succeed();
                            wi.Update(Update.Information("Success"));
                        }
                        else
                        {
                            wi.Update(Update.Warning($"Import Failed [{response.ErrorCode}]: {response.ErrorMessage}"));
                            return "Import Failed";
                        }
                    }
                    break;
                case MediaSource.Sonarr:
                    {
                        wi.Update(Update.Information("Importing into Sonarr"));

                        var response = await sonarrService.ImportEpisodeAsync(wi);
                        if (response.Success)
                        {
                            wiImporter.Succeed();
                            wi.Update(Update.Information("Success"));
                        }
                        else
                        {
                            wi.Update(Update.Warning($"Import Failed [{response.ErrorCode}]: {response.ErrorMessage}"));
                            return "Import Failed";
                        }
                    }
                    break;
                default:
                    {
                        wiImporter.Succeed(false);
                        wi.Update(Update.Error("Source not supported"));
                        return "Source not supported";
                    }
            }

            return null;
        }

        public async Task InitialiseJob(Job job, CancellationToken token)
        {
            using (logger.BeginScope("Initialise Job: {job}", job))
            {
                logger.LogInformation($"Job name: {job.Name}");

                job.LogAction = (update) =>
                {
                    logger.Log(update.Level, update.Message);
                };

                if (job.WorkLoad != null)
                {
                    foreach (var wi in job.WorkLoad.Where(w => w.Condition.Prepare.Processing || w.Condition.Processing.Processing))
                    {
                        if (wi.CancellationToken.CanBeCanceled)
                        {
                            wi.CancellationTokenSource.Cancel();
                        }
                    }
                }

                if (job.Condition.SafeToInitialise)
                {
                    Log(job, Update.Information("Begin Initialisation"));
                    job.UpdateCondition((c) => c.Clear());

                    logger.LogDebug($"Job will initialise.");
                    using var jobInit = new JobWorker(job.Condition.Initialise, job.UpdateStatus);

                    job.Filter = filterManager.GetFilter(job.FilterID);
                    job.Preset = presetManager.GetPreset(job.PresetName);
                    logger.LogDebug($"Job using source: {(job.FilterID != default ? job.FilterID.ToString() : "folder")} ({(job.Filter != null ? job.Filter.Name : job.SourceFolder)}) and preset: {job.PresetName}.");

                    if (job.Preset != null)
                    {
                        var getContainerExtension = await fFmpegProcessor.ConvertContainerToExtension(job.Preset.Container, token);
                        job.Preset.ContainerExtension = getContainerExtension.Result;
                        logger.LogDebug($"Container Extension set to {job.Preset.ContainerExtension}");
                        using var jobTest = new JobWorker(job.Condition.Test, job.UpdateStatus);

                        job.CancellationTokenSource = new();

                        using var lnkCTS = CancellationTokenSource.CreateLinkedTokenSource(job.CancellationToken, applicationService.AppStoppingCancellationToken);
                        try
                        {
                            Log(job, Update.Debug("Begin Testing"));

                            var sourceName = job.MediaSource.ToString();
                            Log(job, Update.Debug($"Job is for {sourceName}, Connecting..."));

                            var systemStatus = job.MediaSource switch
                            {
                                MediaSource.Folder => await folderService.TestConnectionAsync(job.SourceFolder),
                                MediaSource.Radarr => await radarrService.TestConnectionAsync(applicationService.RadarrSettings),
                                MediaSource.Sonarr => await sonarrService.TestConnectionAsync(applicationService.SonarrSettings),
                                _ => new()
                            };

                            if (!systemStatus.Success)
                            {
                                Log(job, Update.Warning($"Failed to connect to {sourceName}. {systemStatus.ErrorMessage}"));
                                Fail(job);
                                return;
                            }

                            Log(job, Update.Debug($"Connected to {sourceName}."), Update.Debug("Fetching List of files."));

                            var getFilesResults = await GetMedia(job);

                            if (!getFilesResults.Success)
                            {
                                Log(job, Update.Warning($"Failed to list files from {sourceName}."));
                                Fail(job);
                                return;
                            }

                            job.WorkLoad = getFilesResults.Results.ToHashSet();


                            Log(job, Update.Debug("Files Returned"), Update.Debug("Building Workload"));

                            using (var jobWorkLoad = new JobWorker(job.Condition.BuildWorkLoad, job.UpdateStatus))
                            {

                                double i = 0;
                                var dirCheck = new ConcurrentBag<string>();
                                var semlock = new SemaphoreSlim(1, 1);
                                try
                                {
                                    await job.WorkLoad.AsyncParallelForEach(wi =>
                                    {
                                        return Task.Run(async () =>
                                        {
                                            using (logger.BeginScope("Work Item {SourceFileName}", wi.SourceFileName))
                                            {
                                                wi.Job = job;
                                                wi.OnUpdate += job.UpdateStatus;
                                                wi.CancellationTokenSource = new();

                                                var file = new FileInfo(wi.SourceFile);

                                                if (!file.Exists)
                                                {
                                                    Log(job, Update.Warning($"This file was not found: {file.FullName}"));
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
                                                    destinationpath = job.MediaSource switch
                                                    {
                                                        MediaSource.Sonarr => Path.Combine(destinationpath, file.Directory.Parent.Name, file.Directory.Name),
                                                        MediaSource.Folder => Path.Combine(destinationpath, Path.GetRelativePath(job.SourceFolder, file.DirectoryName)),
                                                        _ => Path.Combine(destinationpath, file.Directory.Name)
                                                    };
                                                }

                                                var checkFolder = Path.Combine(job.DestinationFolder, file.Directory.Name);
                                                await semlock.WaitAsync(token);
                                                try
                                                {
                                                    if (!dirCheck.Contains(checkFolder))
                                                    {
                                                        if (!Directory.Exists(checkFolder))
                                                        {
                                                            Directory.CreateDirectory(checkFolder);
                                                            Directory.Delete(checkFolder);
                                                            dirCheck.Add(checkFolder);
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    semlock.Release();
                                                }

                                                wi.DestinationFile = Path.Combine(destinationpath, file.Name);
                                                if (!string.IsNullOrWhiteSpace(job.Preset.ContainerExtension))
                                                {
                                                    wi.DestinationFile = Path.ChangeExtension(wi.DestinationFile, job.Preset.ContainerExtension);
                                                }

                                                job.InitialisationProgress?.Report(++i / job.WorkLoad.Count * 100);
                                                job.UpdateStatus(this);
                                            }
                                        });
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Log(job, Update.FromException(ex));
                                    Fail(job);
                                    return;
                                }

                                jobWorkLoad.Succeed();
                                Log(job, Update.Debug("Workload compiled"), Update.Debug("Checking Destination Folder"), Update.Debug("Writing Test.txt file"));
                            }

                            var testFilePath = Path.Combine(job.DestinationFolder, "Test.txt");

                            try
                            {
                                await fileService.WriteTextFileAsync(testFilePath, "This is a write test");

                                fileService.DeleteFile(testFilePath);
                            }
                            catch (Exception ex)
                            {
                                Log(job, Update.FromException(ex));
                                Fail(job);
                                return;
                            }

                            // Success - Job should be good.
                            jobTest.Succeed();
                            Log(job, Update.Information("Test succeeded"));

                            job.ID ??= Guid.NewGuid();
                            jobInit.Succeed();
                            Log(job, Update.Information("Initialisation succeeded"));

                            return;
                        }
                        catch (OperationCanceledException)
                        {
                            Log(job, Update.Warning("Initialisation cancelled"));
                        }
                    }
                    else
                    {
                        Log(job, Update.Debug($"Preset {job.PresetName} does not exist"));
                    }

                    Fail(job);
                }
            }
        }

        public void InitialiseJobs(Filter filter, CancellationToken token)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.Filter == filter))
            {
                _ = InitialiseJob(job, token);
            }
        }

        public void InitialiseJobs(MediaSource source, CancellationToken token)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.MediaSource == source))
            {
                _ = InitialiseJob(job, token);
            }
        }

        public void InitialiseJobs(FFmpegPreset preset, CancellationToken token)
        {
            foreach (var job in Jobs.Where(j => j.Condition.SafeToInitialise && j.Preset == preset))
            {
                _ = InitialiseJob(job, token);
            }
        }

        public async Task PrepareWorkItem(WorkItem wi, FFmpegPreset preset, CancellationToken token)
        {
            using var jobPreparer = new JobWorker(wi.Condition.Prepare, wi.Update);

            var result = await argumentService.SetArguments(preset, wi, token);
            if (!string.IsNullOrWhiteSpace(result))
            {
                wi.Update(Update.Error(result));
                return;
            }

            if (token.IsCancellationRequested) return;

            await CheckHistory(wi);

            jobPreparer.Succeed(wi.Arguments != null && wi.Arguments.Any());
        }

        public bool PresetInUse(FFmpegPreset preset)
        {
            return Jobs.Any(j => j.Preset == preset);
        }

        public async Task ProcessWorkItem(WorkItem wi, CancellationToken token)
        {
            if (!Directory.Exists(Path.GetDirectoryName(wi.DestinationFile)))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(wi.DestinationFile));
                }
                catch (Exception ex)
                {
                    wi.Update(Update.FromException(ex));
                    logger.LogWarning(ex, "Error creating output directory");
                    return;
                }
            }

            var historyID = historyService.StartProcessing(wi.Media.UniqueID, wi.SourceFile, wi.Job.FilterID, wi.Job.PresetName, wi.Arguments);

            if (!wi.Condition.Encode.Processing && !wi.Condition.Encode.Finished)
            {
                using var jobRunner = new JobWorker(wi.Condition.Encode, wi.Update);

                var tokenGranted = false;
                try
                {

                    await processSemaphore.WaitAsync(token);
                    tokenGranted = true;

                    using (logger.BeginScope("FFmpeg Process"))
                    {
                        logger.LogInformation("Starting.");

                        logger.LogDebug("FFmpeg process work item starting.");

                        foreach (var arg in wi.Arguments)
                        {

                            logger.LogDebug($"FFmpeg Process arguments: \"{arg}\"");

                            var result = await fFmpegProcessor.EncodeAVideo(arg, wi.EncoderOnProgress, wi.ProcessStdOut, token);

                            if (!result.Success)
                            {
                                wi.Update(result.Exception);
                                logger.LogInformation($"FFmpeg Process failed.", result.Exception);
                                jobRunner.Succeed(false);
                                return;
                            }

                        }

                        logger.LogInformation($"FFmpeg Process finished."); ;
                        jobRunner.Succeed();
                    }

                }
                catch (OperationCanceledException)
                {
                    Log(wi.Job, Update.Warning("User cancelled job"));
                }

                finally
                {
                    if (tokenGranted)
                        processSemaphore.Release();
                }

                jobRunner.Succeed(false);
            }


            if (wi.Condition.Encode.Succeeded && !wi.Condition.Analyse.Processing && !wi.Condition.Analyse.Finished)
            {
                using var jobAnalyser = new JobWorker(wi.Condition.Analyse, wi.Update);
                try
                {
                    var originalFileSize = new FileInfo(wi.SourceFile).Length;
                    var newFileSize = new FileInfo(wi.DestinationFile).Length;

                    wi.Compression = newFileSize / (decimal)originalFileSize;
                    wi.Update();

                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is IOException)
                {
                    wi.Update(new Update(ex, LogLevel.Warning));
                    logger.LogWarning(ex, "Error fetching file lengths");
                    return;
                }

                if (wi.Job.SSIMCheck || wi.Job.ArgumentCalculationSettings.AlwaysCalculateSSIM)
                {
                    wi.Update("Calculating SSIM");
                    var hardwareDecoder = wi.Job.Preset.HardwareDecoder.Wrap("-hwaccel {0} ");

                    var result = await fFmpegProcessor.CalculateSSIM((args) => wi.UpdateSSIM(args), wi.SourceFile, wi.DestinationFile, hardwareDecoder, token);

                    if (result.Success)
                    {
                        wi.SSIM = result.SSIM;
                    }
                    else
                    {
                        wi.Update(result.Exception);
                        jobAnalyser.Succeed(false);
                    }
                }
                jobAnalyser.Succeed();
            }

            historyService.EndProcessing(historyID, wi.Condition.HappyEncode, wi);

            wi.Update();
        }

        public Job ReloadJob(Job job, CancellationToken token)
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

                _ = InitialiseJob(job, token);

                return job;
            }
        }

        public async void RunJob(Job job)
        {
            using (logger.BeginScope("Run Job: {job}", job))
            {
                if (job.Condition.SafeToRun)
                {
                    try
                    {
                        using var jobCompleter = new JobWorker(job.Condition.Process, job.UpdateStatus);
                        Log(job, Update.Information($"Started Job at: {DateTime.Now}"));


                        foreach (var wi in job.WorkLoad.Where(wi => wi.Condition.ReadyToRun))
                        {
                            using var workItemWorker = new JobWorker(wi.Condition.Processing, wi.Update);
                            try
                            {
                                if (!job.Cancelled)
                                {
                                    wi.CancellationTokenSource = new();
                                    using var lnkCTS = CancellationTokenSource.CreateLinkedTokenSource(wi.CancellationToken, job.CancellationToken, applicationService.AppStoppingCancellationToken);

                                    using (logger.BeginScope("Work Item {SourceFileName}", wi.SourceFileName))
                                    {
                                        Log(job, Update.Information($"Preparing"));

                                        await PrepareWorkItem(wi, job.Preset, lnkCTS.Token);

                                        if (lnkCTS.Token.IsCancellationRequested) return;

                                        if (!wi.Condition.HappyEncode) //skipped if done previously
                                        {
                                            Log(job, Update.Information("Processing"));
                                            await ProcessWorkItem(wi, lnkCTS.Token);
                                        }

                                        if (lnkCTS.Token.IsCancellationRequested) return;

                                        Log(job, Update.Information("Checking Output"));
                                        var checkResultReport = await CheckResult(wi, lnkCTS.Token);
                                        if (checkResultReport != null)
                                        {
                                            Log(job, Update.Warning(checkResultReport));
                                        }

                                        if (lnkCTS.Token.IsCancellationRequested) return;

                                        if (wi.Condition.ReadyForImport && job.AutoImport)
                                        {
                                            Log(job, Update.Information("Importing"));
                                            var importReport = await ImportVideo(wi, job.MediaSource);
                                            if (importReport != null)
                                            {
                                                Log(job, Update.Warning(importReport));
                                            }
                                        }

                                        var ssimUpdate = Update.Information($"Sample Size: {wi.ArgumentCalculator.SampleSize} | SSIM Est. {wi.ArgumentCalculator?.VideoEncoderOptions?.FirstOrDefault()?.AutoPresetTests?.FirstOrDefault(t => t.Best)?.SSIM} | SSIM Act. {wi.SSIM}");
                                        Log(job, ssimUpdate);
                                        wi.Update(ssimUpdate);
                                        workItemWorker.Succeed(wi.Condition.ReadyForImport && (!job.AutoImport || wi.Condition.Import.Succeeded));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                var update = Update.FromException(ex);
                                wi.Update(update);
                                Log(job, update);
                                workItemWorker.Succeed(false);
                            }
                        }

                        jobCompleter.Succeed(!job.Cancelled);
                    }
                    catch (OperationCanceledException)
                    {
                        Log(job, Update.Warning("Job was cancelled"));
                        return;
                    }
                }
            }
        }

        private async Task<string> CheckResult(WorkItem workItem, CancellationToken token)
        {

            using (logger.BeginScope("Checking Results."))
            {
                using var outputChecker = new JobWorker(workItem.Condition.OutputCheck, workItem.Update);
                var result = new WorkItemCheckResult(workItem);

                if (workItem == null)
                {
                    outputChecker.Succeed(false);
                    var msg = "WorkItem is NULL";
                    workItem.Update(new Update(msg, LogLevel.Error));
                    return msg;
                }

                if (workItem.Job.SSIMCheck && workItem.Job.MinSSIM > workItem.SSIM)
                {
                    outputChecker.Succeed(false);
                    var msg = $"File similarity below threshold ({workItem.SSIM.ToPercent(2).Adorn("%")})";
                    workItem.Update(new Update(msg, LogLevel.Warning));
                    return msg;
                }

                if (workItem.Job.SizeCheck && workItem.Job.MaxCompression < workItem.Compression)
                {
                    outputChecker.Succeed(false);
                    var msg = $"File size above threshold ({workItem.Compression.ToPercent(2).Adorn("%")})";
                    workItem.Update(new Update(msg, LogLevel.Warning));
                    return msg;
                }

                var ffProbeResponse = await fFmpegProcessor.GetFFProbeInfo(workItem.DestinationFile, token);
                if (ffProbeResponse.Success)
                {
                    var mediaInfo = ffProbeResponse.Result;

                    //Workitem.Duration refers to the processing time frame.
                    logger.LogDebug($"Original Duration: {workItem.TotalLength}");
                    logger.LogDebug($"New Duration: {mediaInfo?.format?.Duration}");
                    workItem.Output(new($"Original Duration: {workItem.TotalLength}"));
                    workItem.Output(new($"New Duration: {mediaInfo?.format?.Duration}"));

                    if (mediaInfo?.format?.Duration == default)
                    {
                        outputChecker.Succeed(false);
                        var msg = "FFprobe failed to analyse the output";
                        workItem.Update(new Update(msg, LogLevel.Warning));
                        return msg;
                    }

                    if (workItem.TotalLength.HasValue &&
                        Math.Abs(mediaInfo.format.Duration.TotalSeconds - workItem.TotalLength.Value.TotalSeconds) > 2) //Check to the nearest second.
                    {
                        outputChecker.Succeed(false);
                        var msg = $"Video duration mismatch by {TimeSpan.FromSeconds(Math.Abs(mediaInfo.format.Duration.TotalSeconds - workItem.TotalLength.Value.TotalSeconds)).Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second)}";
                        workItem.Update(new Update(msg, LogLevel.Warning));
                        return msg;
                    }
                }
                else
                {
                    outputChecker.Succeed(false);
                    var msg = "FFprobe failed to analyse the output";
                    workItem.Update(new Update(msg, LogLevel.Warning));
                    return msg;
                }

                outputChecker.Succeed();
                return null;
            }
        }
        private void Fail(Job job)
        {
            Log(job, Update.Warning("Test failed"));
        }

        private async Task<ServiceResult<HashSet<WorkItem>>> GetMedia(Job job)
        {
            using (logger.BeginScope("Get Files"))
            {

                if (job.MediaSource == MediaSource.Radarr)
                {
                    logger.LogInformation("From Radarr");

                    var filter = filterManager.ConstructFilterQuery(job.Filter.Filters, out var filterVals);

                    var getMoviesResponse = await radarrService.RequestMoviesFilteredAsync(filter, filterVals);

                    if (!getMoviesResponse.Success)
                    {
                        return new(false, getMoviesResponse.ErrorCode, getMoviesResponse.ErrorMessage);
                    }

                    var movies = getMoviesResponse.Results;
                    return new(true, movies.Select(x => new WorkItem(x)).ToHashSet());
                }

                if (job.MediaSource == MediaSource.Sonarr)
                {
                    logger.LogInformation("From Sonarr");

                    var filter = filterManager.ConstructFilterQuery(job.Filter.Filters, out var filterVals);

                    var getSeriesResponse = await sonarrService.RequestSeriesFilteredAsync(filter, filterVals);

                    if (!getSeriesResponse.Success)
                    {
                        return new(false, getSeriesResponse.ErrorCode, getSeriesResponse.ErrorMessage);
                    }

                    var series = getSeriesResponse.Results;
                    return new(true, series.SelectMany(s => s.Seasons).SelectMany(s => s.EpisodeFiles).Select(x => new WorkItem(x)).ToHashSet());
                }

                if (job.MediaSource == MediaSource.Folder)
                {
                    logger.LogInformation($"From a Folder: {job.SourceFolder}");

                    var getFilesResponse = await folderService.RequestFilesAsync(job.SourceFolder);

                    if (!getFilesResponse.Success)
                    {
                        return new(false, getFilesResponse.ErrorCode, getFilesResponse.ErrorMessage);
                    }

                    var files = getFilesResponse.Results;
                    return new(true, files.Select(x => new WorkItem(x)).ToHashSet());
                }


                logger.LogWarning($"Source ({job.MediaSource}) is not supported");
                return new(false, "", "Not Implemented");
            }
        }

        private void Log(Job job, params Update[] updates) //, params string[] messages)
        {
            foreach (var u in updates.Where(x => !string.IsNullOrWhiteSpace(x.Message)))
            {
                logger.Log(u.Level, $"Job {job.Name}: {u.Message}.", null);
                job.Log(u);
            }
        }
    }
}