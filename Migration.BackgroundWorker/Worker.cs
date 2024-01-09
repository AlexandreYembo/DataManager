using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Migration.BackgroundWorker.Services;

namespace Migration.BackgroundWorker
{
    public class Worker : BackgroundService
    {
        public IMigrationService _migrationService;

        private readonly ILogger<Worker> _logger;

        public Worker(IMigrationService migrationService,
            ILogger<Worker> logger)
        {
            _migrationService = migrationService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Service is starting....");

            stoppingToken.Register(() => _logger.LogDebug("Background Service is stopping...."));

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                _migrationService.Process()

                  await Task.Delay(5000, stoppingToken);
            }
        }
    }
}