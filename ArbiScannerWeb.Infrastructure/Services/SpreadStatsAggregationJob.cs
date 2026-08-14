using ArbiScannerWeb.Abstractions.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ArbiScannerWeb.Infrastructure.Services
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900])]
    public class SpreadStatsAggregationJob
    {
        private readonly ISpreadStatsService _statsService;
        private readonly ILogger<SpreadStatsAggregationJob> _logger;

        public SpreadStatsAggregationJob(ISpreadStatsService statsService, ILogger<SpreadStatsAggregationJob> logger)
        {
            _statsService = statsService;
            _logger = logger;
        }

        public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Running spread stats aggregation");

            var result = await _statsService.GenerateAndPersistSnapshotAsync();

            if (result.IsFailed)
                _logger.LogError("Failed to generate spread stats snapshot: {Errors}", result.Errors);
            else
                _logger.LogInformation(
                    "Generated spread stats snapshot {Id} from {Count} spreads",
                    result.Value.Id, result.Value.TotalSpreadsAnalyzed);
        }
    }
}
