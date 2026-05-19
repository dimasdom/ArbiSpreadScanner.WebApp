using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IDatabase _redis;
        private readonly string _adminApiUrl;
        private const string TokenCacheKey = "admin_service:jwt_token";
        private readonly SemaphoreSlim _authLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IConfiguration configuration,
            IConnectionMultiplexer redis,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminService> logger)
        {
            _adminApiUrl = configuration["AdminApiUrl"]
                ?? throw new InvalidOperationException("AdminApiUrl configuration is required");
            _configuration = configuration;
            _redis = redis.GetDatabase();
            _httpClient = httpClientFactory.CreateClient("AdminApi");
            _logger = logger;
        }

        private async Task EnsureAuthenticatedAsync()
        {
            var cached = await _redis.StringGetAsync(TokenCacheKey);
            if (cached.HasValue)
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", cached.ToString());
                return;
            }

            await RefreshTokenAsync();
        }

        private async Task RefreshTokenAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock — another thread may have refreshed already
                var cached = await _redis.StringGetAsync(TokenCacheKey);
                if (cached.HasValue)
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", cached.ToString());
                    return;
                }

                var adminUser = _configuration.GetSection("AdminUser");
                var loginRequest = new { userName = adminUser["UserName"], password = adminUser["Password"] };
                var content = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_adminApiUrl}/api/account/Authenticate", content);
                if (!response.IsSuccessStatusCode) return;

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var valueElement) &&
                    valueElement.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        await _redis.StringSetAsync(TokenCacheKey, token, TimeSpan.FromHours(23));
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", token);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh admin token");
            }
            finally
            {
                _authLock.Release();
            }
        }

        private async Task<Result<T>> GetResultAsync<T>(string url)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await _redis.KeyDeleteAsync(TokenCacheKey);
                    await RefreshTokenAsync();
                    response = await _httpClient.GetAsync(url);
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    if (root.TryGetProperty("value", out var valueElement))
                        return Result.Ok(JsonSerializer.Deserialize<T>(valueElement.GetRawText(), options)!);

                    return Result.Ok(JsonSerializer.Deserialize<T>(responseBody, options)!);
                }

                return Result.Fail<T>($"HTTP Error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from {Url}", url);
                return Result.Fail<T>($"Error fetching data: {ex.Message}");
            }
        }

        private async Task<Result<T>> PostResultAsync<T>(string url, object postData)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var content = new StringContent(JsonSerializer.Serialize(postData), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await _redis.KeyDeleteAsync(TokenCacheKey);
                    await RefreshTokenAsync();
                    content = new StringContent(JsonSerializer.Serialize(postData), Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(url, content);
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Result<T>>(responseBody);
                    return result ?? Result.Fail<T>("Failed to deserialize response");
                }

                return Result.Fail<T>($"HTTP Error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting data to {Url}", url);
                return Result.Fail<T>($"Error posting data: {ex.Message}");
            }
        }

        public async Task<Result<UserSubscriptionPayment>> CreatePaymentAsync(string userId, int subscriptionId)
        {
            var url = $"{_adminApiUrl}/api/payments/CreatePaymentForUser";
            var payload = new { UserId = userId, SubscriptionId = subscriptionId };
            return await PostResultAsync<UserSubscriptionPayment>(url, payload);
        }

        public async Task<Result<List<SubscriptionModel>>> GetAllSubscriptionsAsync()
        {
            var url = $"{_adminApiUrl}/api/subscriptions/GetAllSubscriptions?page=1";
            var result = await GetResultAsync<SubscriptionModel[]>(url);
            if (result.IsSuccess)
                return Result.Ok(result.Value.ToList());
            return Result.Fail<List<SubscriptionModel>>(result.Errors);
        }

        public Task<Result<UserSubscriptionPayment>> GetPaymentStatusAsync(int paymentId)
        {
            var url = $"{_adminApiUrl}/api/payments/GetUserPaymentByIdAsync?paymentId={WebUtility.UrlEncode(paymentId.ToString())}";
            return GetResultAsync<UserSubscriptionPayment>(url);
        }

        public Task<Result<SubscriptionModel>> GetSubscriptionDetailsAsync(int subscriptionId)
        {
            var url = $"{_adminApiUrl}/api/subscriptions/GetSubscriptionById?id={WebUtility.UrlEncode(subscriptionId.ToString())}";
            return GetResultAsync<SubscriptionModel>(url);
        }

        public Task<Result<UserSubscriptionPayment>> GetUserActivePaymentsAsync(string userId)
        {
            var url = $"{_adminApiUrl}/api/payments/GetActivePaymentForUser?userId={WebUtility.UrlEncode(userId)}";
            return GetResultAsync<UserSubscriptionPayment>(url);
        }

        public Task<Result<UserSubscriptionModel>> GetUserActiveSubscriptionsAsync(string userId)
        {
            var url = $"{_adminApiUrl}/api/subscriptions/GetUserSubscriptionByUserId?userId={WebUtility.UrlEncode(userId)}";
            return GetResultAsync<UserSubscriptionModel>(url);
        }

        public async Task<Result> CancelPayment(int userSubscriptionPaymentId)
        {
            var url = $"{_adminApiUrl}/api/payments/CancelPayment?userSubscriptionPaymentId={WebUtility.UrlEncode(userSubscriptionPaymentId.ToString())}";
            var res = await PostResultAsync<object>(url, new { });
            return res.IsSuccess ? Result.Ok() : Result.Fail(res.Errors);
        }
    }
}
