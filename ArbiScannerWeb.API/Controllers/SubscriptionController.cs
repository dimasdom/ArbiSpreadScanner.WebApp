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
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [AllowAnonymous]
        [HttpGet("GetAllSubscriptions")]
        public async Task<Result<List<SubscriptionModel>>> GetSubscriptions()
        {
            return (await _subscriptionService.GetAllSubscriptionsAsync()).ToSerializable();
        }
        
        [HttpGet("GetPaymentStatus")]
        public async Task<Result<UserSubscriptionPayment>> GetPaymentStatus([FromQuery] int userSubscriptionPaymentId)
        {
            return (await _subscriptionService.GetPaymentStatusAsync(userSubscriptionPaymentId)).ToSerializable();
        }

        [HttpGet("GetSubscriptionDetails")]
        public async Task<Result<SubscriptionModel>> GetSubscriptionDetails([FromQuery] int subscriptionId)
        {
            return (await _subscriptionService.GetSubscriptionDetailsAsync(subscriptionId)).ToSerializable();
        }

        [HttpPost("CreatePayment")]
        public async Task<Result<UserSubscriptionPayment>> CreatePayment([FromQuery] int subscriptionId)
        {
            return (await _subscriptionService.CreatePaymentAsync(subscriptionId)).ToSerializable();
        }

        [HttpGet("GetUserActiveSubscriptions")]
        public async Task<Result<UserSubscriptionModelDTO>> GetUserActiveSubscriptions()
        {
            return (await _subscriptionService.GetUserActiveSubscriptionsAsync()).ToSerializable();
        }

        [HttpGet("GetUserActivePayments")]
        public async Task<Result<UserSubscriptionPayment>> GetUserActivePayments()
        {
            return (await _subscriptionService.GetUserActivePaymentsAsync()).ToSerializable();
        }

        [HttpPost("CancelPayment")]
        public async Task<Result> CancelPayment([FromQuery] int userSubscriptionPaymentId)
        {
            return await _subscriptionService.CancelPayment(userSubscriptionPaymentId);
        }
    }
}
