using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing;
using Compressarr.Pages.Services;
using Compressarr.Application;
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

        //private readonly IApplicationInitialiser fFmpegInitialiser;


        public AsyncInitializationMiddleware(RequestDelegate next,  IHostApplicationLifetime lifetime, ILogger<AsyncInitializationMiddleware> logger, IApplicationService settingsManager)
        {
            //this.fFmpegInitialiser = fFmpegInitialiser;
            this.logger = logger;
            this.next = next;

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

                    //fFmpegInitialiser.OnReady += (o, e) => logger.LogInformation("FFmpeg Ready, Initialisation complete");
                    //_ = fFmpegInitialiser.Start();

                    logger.LogDebug("Initialization started");


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
