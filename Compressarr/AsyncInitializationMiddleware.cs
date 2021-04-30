using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing;
using Compressarr.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr
{
    public class AsyncInitializationMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger logger;
        private Task _initializationTask;

        private readonly IFFmpegInitialiser fFmpegInitialiser;
        private readonly IFFmpegManager fFmpegManager;
        private readonly IJobManager jobManager;
        private readonly ISettingsManager settingsManager;
        

        public AsyncInitializationMiddleware(RequestDelegate next, IFFmpegInitialiser fFmpegInitialiser, IFFmpegManager fFmpegManager, IHostApplicationLifetime lifetime, IJobManager jobManager, ILogger<AsyncInitializationMiddleware> logger, ISettingsManager settingsManager)
        {
            this.fFmpegInitialiser = fFmpegInitialiser;
            this.fFmpegManager = fFmpegManager;
            this.jobManager = jobManager;
            this.logger = logger;
            this.next = next;
            this.settingsManager = settingsManager;

            // Start initialization when the app starts
            var startRegistration = default(CancellationTokenRegistration);
            startRegistration = lifetime.ApplicationStarted.Register(() =>
            {
                _initializationTask = InitializeAsync(lifetime.ApplicationStopping);
                startRegistration.Dispose();
            });
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using (logger.BeginScope("Async Initialization"))
            {
                try
                {
                    logger.LogInformation("Initialization starting");

                    await fFmpegInitialiser.Start();

                    foreach (var job in settingsManager.Jobs)
                    {
                        _ = jobManager.InitialiseJob(job);
                    }

                    foreach (var preset in settingsManager.Presets)
                    {
                        await fFmpegManager.InitialisePreset(preset);
                    }

                    logger.LogInformation("Initialization complete");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Initialization failed");
                    throw;
                }
            }
        }

        public async Task Invoke(HttpContext context)
        {
            // Take a copy to avoid race conditions
            var initializationTask = _initializationTask;
            if (initializationTask != null)
            {
                // Wait until initialization is complete before passing the request to next middleware
                await initializationTask;

                // Clear the task so that we don't await it again later.
                _initializationTask = null;
            }

            // Pass the request to the next middleware
            await next(context);
        }
    }
}
