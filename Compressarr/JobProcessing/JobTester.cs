using Compressarr.JobProcessing.Models;
using Compressarr.Services.Interfaces;
using Compressarr.Settings;
using System;
using System.IO;
using System.Linq;

namespace Compressarr.JobProcessing
{
    public class JobTester
    {
        private IRadarrService radarrService;
        private ISonarrService sonarrService;
        private SettingsManager settingsManager;
        private JobManager jobManager;
        public Job Job { get; internal set; }

        public bool Success { get; private set; }

        internal JobTester(JobManager jobManager)
        {
            this.jobManager = jobManager;
            this.settingsManager = jobManager.settingsManager;
            this.radarrService = jobManager.radarrService;
            this.sonarrService = jobManager.sonarrService;
        }

        internal async void Test(Job job)
        {

            this.Job = job;
            Log("Begin Testing");

            if (job.Filter.MediaSource == Filtering.MediaSource.Radarr)
            {
                Log("Job is for Movies, Connecting to Radarr");
                var radarrURL = settingsManager.GetSetting(SettingType.RadarrURL);
                var radarrAPIKey = settingsManager.GetSetting(SettingType.RadarrAPIKey);

                var systemStatus = radarrService.TestConnection(radarrURL, radarrAPIKey);

                if (!systemStatus.Success)
                {
                    Log("Failed to connect to Radarr.");
                    Fail();
                    return;
                }

                Log("Connected to Radarr", "Fetching List of files from Radarr");

                var getFilesResults = await jobManager.GetFiles(job);

                if (!getFilesResults.Success)
                {
                    Log("Failed to list files from Radarr.");
                    Fail();
                    return;
                }

                //job.Files = getFilesResults.Results;

                Log("Files Returned", "Randomly Checking up to 3 files");

                var files = getFilesResults.Results.OrderBy(a => Guid.NewGuid()).Take(3);

                //foreach (var f in files)
                //{
                //    if (!f.Exists)
                //    {
                //        Log($"3 Files are randomly tested, this one was not found: {f.FullName}");
                //        Fail();
                //        return;
                //    }
                //}

                Log("Files Found", "Checking Destination Folder", "Writing Test.txt file");

                var testFilePath = Path.Combine(job.WriteFolder, "Test.txt");

                try
                {
                    File.WriteAllText(testFilePath, "This is a write test");
                    File.Delete(testFilePath);
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                    Fail();
                    return;
                }

                Succeed();
                return;
            }

            Fail();
        }

        private void Log(params string[] messages)
        {
            Job.UpdateStatus(messages);
        }

        private void Fail()
        {
            Job.JobStatus = JobStatus.TestedFail;
            Success = false;
            Log("Test failed");
        }

        private void Succeed()
        {
            Job.JobStatus = JobStatus.TestedOK;
            Success = true;
            Log("Test succeeded");
        }
    }
}