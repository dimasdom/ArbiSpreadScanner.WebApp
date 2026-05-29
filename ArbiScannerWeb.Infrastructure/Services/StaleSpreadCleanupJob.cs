using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ArbiScannerWeb.Infrastructure.Services
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900])]
    public class StaleSpreadCleanupJob
    {
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(4);

        private readonly ITradeOpportunityRepository _spreadRepo;
        private readonly ITradeOpportunityService _spreadService;
        private readonly ITradeOpportunityTickerRepository _tickerRepo;
        private readonly ILogger<StaleSpreadCleanupJob> _logger;

        public StaleSpreadCleanupJob(
            ITradeOpportunityRepository spreadRepo,
            ITradeOpportunityService spreadService,
            ITradeOpportunityTickerRepository tickerRepo,
            ILogger<StaleSpreadCleanupJob> logger)
        {
            _spreadRepo = spreadRepo;
            _spreadService = spreadService;
            _tickerRepo = tickerRepo;
            _logger = logger;
        }

        public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
        {
            _logger.LogInformation("Running stale spread cleanup at midnight UTC");

            var cutoff = DateTime.UtcNow - StaleThreshold;
            var allOpenSpreads = new List<TradeOpportunityModel>();

            foreach (var type in Enum.GetValues<SpreadType>())
            {
                var spreads = await _spreadRepo.GetOpenByTypeAsync(type);
                allOpenSpreads.AddRange(spreads);
            }

            var staleSpreads = allOpenSpreads.Where(s => s.DateTime < cutoff).ToList();

            _logger.LogInformation(
                "Found {Count} stale open spreads not updated in {Hours} hours",
                staleSpreads.Count, StaleThreshold.TotalHours);

            foreach (var spread in staleSpreads)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _spreadService.CloseSpread(spread);

                if (result.IsFailed)
                    _logger.LogError(
                        "Failed to close stale spread {Guid}: {Errors}",
                        spread.Guid, result.Errors);
                else
                    _logger.LogInformation(
                        "Closed stale spread {Guid} (last updated {LastUpdated:O})",
                        spread.Guid, spread.DateTime);
            }

            // Delete tickers whose spread is no longer open (orphaned by the
            // fire-and-forget race in MessageProcessingService, or by prior bugs).
            var openGuids = allOpenSpreads
                .Where(s => !staleSpreads.Any(stale => stale.Guid == s.Guid))
                .Select(s => s.Guid)
                .ToList();

            var deleted = await _tickerRepo.DeleteOrphanedAsync(openGuids);
            if (deleted > 0)
                _logger.LogInformation("Deleted {Count} orphaned ticker(s) for closed spreads", deleted);
        }
    }
}
