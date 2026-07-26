using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/{controller}")]
    public class TelegramLinkController : ControllerBase
    {
        private readonly IUserSettingsService _telegramUserService;

        public TelegramLinkController(IUserSettingsService telegramUserService)
        {
            _telegramUserService = telegramUserService;
        }

        [HttpPost("CreateTelegramLinkRequest")]
        public async Task<ActionResult<Result<TelegramLinkRequest>>> CreateTelegramLinkRequest()
        {
            return (await _telegramUserService.CreateLinkRequestAsyncForAuthUser()).ToSerializable();
        }

        [HttpPost("RemoveTelegramLink")]
        public async Task<ActionResult<Result>> RemoveTelegramLink()
        {
            return (await _telegramUserService.RemoveTelegramLinkAsyncForAuthUser()).ToSerializable();
        }
    }
}
