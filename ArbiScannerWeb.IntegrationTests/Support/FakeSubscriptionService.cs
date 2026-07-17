using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;

namespace ArbiScannerWeb.IntegrationTests.Support;

internal sealed class FakeSubscriptionService : ISubscriptionService
{
    public Task<Result<List<SubscriptionModel>>> GetAllSubscriptionsAsync()
        => Task.FromResult(Result.Ok(new List<SubscriptionModel>()));

    public Task<Result<UserSubscriptionPayment>> GetPaymentStatusAsync(int userSubscriptionPaymentId)
        => Task.FromResult(Result.Ok(new UserSubscriptionPayment()));

    public Task<Result<SubscriptionModel>> GetSubscriptionDetailsAsync(int subscriptionId)
        => Task.FromResult(Result.Fail<SubscriptionModel>("not seeded in this fixture"));

    public Task<Result<UserSubscriptionPayment>> CreatePaymentAsync(int subscriptionId)
        => Task.FromResult(Result.Ok(new UserSubscriptionPayment()));

    public Task<Result<UserSubscriptionModelDTO>> GetUserActiveSubscriptionsAsync()
        => Task.FromResult(Result.Ok(new UserSubscriptionModelDTO { IsActive = true }));

    public Task<Result<UserSubscriptionPayment>> GetUserActivePaymentsAsync()
        => Task.FromResult(Result.Ok(new UserSubscriptionPayment()));

    public Task<Result> CancelPayment(int userSubscriptionPaymentId)
        => Task.FromResult(Result.Ok());

    public Task<bool> CheckIfUserHasActiveSubscriptionAsync() => Task.FromResult(true);
}
