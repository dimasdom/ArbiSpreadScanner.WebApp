using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers
{
    // Login/Register/ForgotPassword/ResetPassword/ConfirmEmail/Refresh/Logout are
    // gone — Keycloak (arbiscanner-web realm) owns the whole auth lifecycle now.
    // What's left just reads/writes local business data for the authenticated,
    // JIT-provisioned user (see JitUserProvisioningMiddleware).
    [ApiController]
    [Authorize]
    [Route("api/{controller}")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost("UpdateDetails")]
        public async Task<ActionResult<Result<AccountEditDto>>> UpdateDetails([FromBody] AccountEditDto account)
        {
            return (await _accountService.UpdateDetails(account)).ToSerializable();
        }

        [HttpGet("GetUserData")]
        public async Task<ActionResult<Result<AccountDto>>> GetUserData()
        {
            return (await _accountService.GetUserData()).ToSerializable();
        }
    }
}
