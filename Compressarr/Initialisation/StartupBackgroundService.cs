using Compressarr.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Initialisation
{
    public class StartupBackgroundService : BackgroundService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StartupBackgroundService> _logger;
        private readonly IApplicationInitialiser _applicationInitialiser;

        public StartupBackgroundService(IServiceProvider serviceProvider, ILogger<StartupBackgroundService> logger, IApplicationInitialiser applicationInitialiser) 
            => (_serviceProvider, _logger, _applicationInitialiser) 
            = (serviceProvider, logger, applicationInitialiser);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(StartupBackgroundService)} is running.");

            await DoWorkAsync(stoppingToken);
        }

        private async Task DoWorkAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(StartupBackgroundService)} is working.");

            await _applicationInitialiser.InitialiseAsync();
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{nameof(StartupBackgroundService)} is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}
