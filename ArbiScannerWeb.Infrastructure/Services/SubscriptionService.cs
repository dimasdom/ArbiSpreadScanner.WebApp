using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Abstractions;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IAdminService _adminService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDatabase _redis;
        public SubscriptionService(IAdminService adminService, IHttpContextAccessor httpContextAccessor, IConnectionMultiplexer redis)
        {
            _adminService = adminService;
            _httpContextAccessor = httpContextAccessor;
            _redis = redis.GetDatabase();
        }

        public async Task<Result> CancelPayment(int userSubscriptionPaymentId)
        {
            return await _adminService.CancelPayment(userSubscriptionPaymentId);
        }

        public async Task<bool> CheckIfUserHasActiveSubscriptionAsync()
        {
            var userSubRes = await GetUserActiveSubscriptionsAsync();
            return (userSubRes.IsSuccess && userSubRes.Value != null) && userSubRes.Value.EndDate > DateTime.UtcNow;
        }

        public async Task<Result<UserSubscriptionPayment>> CreatePaymentAsync(int subscriptionId)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (userId is null)
                return Result.Fail<UserSubscriptionPayment>("User is not authenticated.");
            var res  = await _adminService.CreatePaymentAsync(userId, subscriptionId);
            if (res.IsSuccess)
            {
                return Result.Ok(res.Value);
            }
            return Result.Fail<UserSubscriptionPayment>(res.Errors);
        }

        public async Task<Result<List<SubscriptionModel>>> GetAllSubscriptionsAsync()
        {
            var res = await _adminService.GetAllSubscriptionsAsync();
            if (res.IsSuccess)
            {
                return Result.Ok(res.Value);
            }
            return Result.Fail<List<SubscriptionModel>>(res.Errors);
        }

        public async Task<Result<UserSubscriptionPayment>> GetPaymentStatusAsync(int userSubscriptionPaymentId)
        {
            var res = await _adminService.GetPaymentStatusAsync(userSubscriptionPaymentId);
            if (res.IsSuccess)
            {
                return Result.Ok(res.Value);
            }
            return Result.Fail<UserSubscriptionPayment>(res.Errors);
        }

        public async Task<Result<SubscriptionModel>> GetSubscriptionDetailsAsync(int subscriptionId)
        {
            var res = await _adminService.GetSubscriptionDetailsAsync(subscriptionId);
            if (res.IsSuccess)
            {
                return Result.Ok(res.Value);
            }
            return Result.Fail<SubscriptionModel>(res.Errors);
        }

        public async Task<Result<UserSubscriptionPayment>> GetUserActivePaymentsAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (userId is null)
                return Result.Fail<UserSubscriptionPayment>("User is not authenticated.");
            var res = await _adminService.GetUserActivePaymentsAsync(userId);
            if (res.IsSuccess)
            {
                return Result.Ok(res.Value);
            }
            return Result.Fail<UserSubscriptionPayment>(res.Errors);
        }

        public async Task<Result<UserSubscriptionModelDTO>> GetUserActiveSubscriptionsAsync()
        {
            
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (userId is null)
                return Result.Fail<UserSubscriptionModelDTO>("User is not authenticated.");
            var user = await _redis.StringGetAsync($"userSubscription:{userId}");
            if (!user.IsNullOrEmpty)
            {
                var userSubDto = Newtonsoft.Json.JsonConvert.DeserializeObject<UserSubscriptionModel>((string)user!);
                if (userSubDto != null)
                {
                    return Result.Ok(new UserSubscriptionModelDTO(userSubDto));
                }
            }
            var res = await _adminService.GetUserActiveSubscriptionsAsync(userId);
            if (res.IsSuccess)
            {
                var userSubDto = res.Value is not null ? new UserSubscriptionModelDTO(res.Value)
                : new UserSubscriptionModelDTO()
                {
                    IsActive = false
                };
                await _redis.StringSetAsync($"userSubscription:{userId}", Newtonsoft.Json.JsonConvert.SerializeObject(res.Value), TimeSpan.FromHours(24));
                return Result.Ok(userSubDto);
            }
        
            return Result.Fail<UserSubscriptionModelDTO>(res.Errors);
        }
        
    }
}