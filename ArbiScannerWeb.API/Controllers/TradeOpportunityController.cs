using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/{controller}")]
    public class TradeOpportunityController : ControllerBase
    {
        private readonly ITradeOpportunityService _tradeOpportunityService;

        public TradeOpportunityController(ITradeOpportunityService tradeOpportunityService)
        {
            _tradeOpportunityService = tradeOpportunityService;
        }

        [HttpGet("GetSpreadsForUser")]
        public async Task<ActionResult<SerializableResult<List<TradeOpportunityDetailsDto>>>> GetSpreadsForUser()
        {
            var userId = User.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(Result.Fail("User ID claim not found").ToResult<List<TradeOpportunityDetailsDto>>().ToSerializable());
            }

            var result = await _tradeOpportunityService.GetSpreadsForUser(userId);
            if (result.IsFailed)
            {
                return Ok(result.ToSerializable());
            }

            return Ok(result.ToSerializable());
        }

        [HttpGet("GetSpreadInfo/{id}")]
        public async Task<ActionResult<SerializableResult<TradeOpportunityDetailsDto>>> GetSpreadInfo(string id)
        {
            var result = await _tradeOpportunityService.GetSpreadInfo(id);

            if (result.IsFailed)
            {
                return Ok(result.ToSerializable());
            }

            return Ok(result.ToSerializable());
        }
    }
}
