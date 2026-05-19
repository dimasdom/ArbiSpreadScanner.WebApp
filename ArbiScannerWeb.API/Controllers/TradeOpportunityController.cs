using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        public async Task<ActionResult<SerializableResult<List<TradeOpportunityDetailsDTO>>>> GetSpreadsForUser()
        {
            var userclaims = User.Claims;
            var userId = userclaims.FirstOrDefault(x => x.Type == ClaimsIdentity.DefaultNameClaimType);
            if (userId is null)
            {
                return BadRequest(Result.Fail("User ID claim not found").ToResult<List<TradeOpportunityDetailsDTO>>().ToSerializable());
            }

            var result = await _tradeOpportunityService.GetSpreadsForUser(userId.Value);
            if (result.IsFailed)
            {
                return Ok(result.ToSerializable());
            }

            return Ok(result.ToSerializable());
        }

        [HttpGet("GetSpreadInfo/{id}")]
        public async Task<ActionResult<SerializableResult<TradeOpportunityDetailsDTO>>> GetSpreadInfo(string id)
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
