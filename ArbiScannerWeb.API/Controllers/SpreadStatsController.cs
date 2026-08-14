using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers
{
    // Public: stats are aggregate/anonymized and intentionally viewable without login or a
    // subscription, unlike TradeOpportunityController's per-user spread data.
    [ApiController]
    [Route("api/{controller}")]
    public class SpreadStatsController : ControllerBase
    {
        private readonly ISpreadStatsService _statsService;

        public SpreadStatsController(ISpreadStatsService statsService)
        {
            _statsService = statsService;
        }

        [HttpGet("GetLatest")]
        public async Task<ActionResult<SerializableResult<SpreadStatsSnapshotModel>>> GetLatest()
        {
            var result = await _statsService.GetLatestAsync();
            return Ok(result.ToSerializable());
        }

        [HttpGet("GetById")]
        public async Task<ActionResult<SerializableResult<SpreadStatsSnapshotModel>>> GetById([FromQuery] Guid id)
        {
            var result = await _statsService.GetByIdAsync(id);
            return Ok(result.ToSerializable());
        }

        [HttpGet("GetSnapshotIndex")]
        public async Task<ActionResult<SerializableResult<List<SnapshotIndexEntry>>>> GetSnapshotIndex()
        {
            var result = await _statsService.GetSnapshotIndexAsync();
            return Ok(result.ToSerializable());
        }
    }
}
