using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentAssertions;
using FluentResults;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class SubscriptionControllerTests
{
    private readonly Mock<ISubscriptionService> _subscriptionService = new();
    private readonly SubscriptionController _sut;

    public SubscriptionControllerTests()
    {
        _sut = new SubscriptionController(_subscriptionService.Object);
    }

    [Fact]
    public async Task GetSubscriptions_Success_ReturnsList()
    {
        var list = new List<SubscriptionModel> { new() { Id = 1 } };
        _subscriptionService.Setup(s => s.GetAllSubscriptionsAsync()).ReturnsAsync(Result.Ok(list));

        var response = (SerializableResult<List<SubscriptionModel>>)await _sut.GetSubscriptions();

        response.IsSuccess.Should().BeTrue();
        response.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetSubscriptions_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.GetAllSubscriptionsAsync())
            .ReturnsAsync(Result.Fail<List<SubscriptionModel>>("boom"));

        var response = (SerializableResult<List<SubscriptionModel>>)await _sut.GetSubscriptions();

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetPaymentStatus_Success_ReturnsPayment()
    {
        var payment = new UserSubscriptionPayment { Id = 5 };
        _subscriptionService.Setup(s => s.GetPaymentStatusAsync(5)).ReturnsAsync(Result.Ok(payment));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.GetPaymentStatus(5);

        response.IsSuccess.Should().BeTrue();
        response.Value!.Id.Should().Be(5);
    }

    [Fact]
    public async Task GetPaymentStatus_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.GetPaymentStatusAsync(5))
            .ReturnsAsync(Result.Fail<UserSubscriptionPayment>("not found"));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.GetPaymentStatus(5);

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetSubscriptionDetails_Success_ReturnsSubscription()
    {
        var sub = new SubscriptionModel { Id = 3 };
        _subscriptionService.Setup(s => s.GetSubscriptionDetailsAsync(3)).ReturnsAsync(Result.Ok(sub));

        var response = (SerializableResult<SubscriptionModel>)await _sut.GetSubscriptionDetails(3);

        response.IsSuccess.Should().BeTrue();
        response.Value!.Id.Should().Be(3);
    }

    [Fact]
    public async Task GetSubscriptionDetails_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.GetSubscriptionDetailsAsync(3))
            .ReturnsAsync(Result.Fail<SubscriptionModel>("not found"));

        var response = (SerializableResult<SubscriptionModel>)await _sut.GetSubscriptionDetails(3);

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePayment_Success_ReturnsPayment()
    {
        var payment = new UserSubscriptionPayment { Id = 9 };
        _subscriptionService.Setup(s => s.CreatePaymentAsync(2)).ReturnsAsync(Result.Ok(payment));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.CreatePayment(2);

        response.IsSuccess.Should().BeTrue();
        response.Value!.Id.Should().Be(9);
    }

    [Fact]
    public async Task CreatePayment_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.CreatePaymentAsync(2))
            .ReturnsAsync(Result.Fail<UserSubscriptionPayment>("bad request"));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.CreatePayment(2);

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_Success_ReturnsDto()
    {
        var dto = new UserSubscriptionModelDto { IsActive = true };
        _subscriptionService.Setup(s => s.GetUserActiveSubscriptionsAsync()).ReturnsAsync(Result.Ok(dto));

        var response = (SerializableResult<UserSubscriptionModelDto>)await _sut.GetUserActiveSubscriptions();

        response.IsSuccess.Should().BeTrue();
        response.Value!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.GetUserActiveSubscriptionsAsync())
            .ReturnsAsync(Result.Fail<UserSubscriptionModelDto>("unauthenticated"));

        var response = (SerializableResult<UserSubscriptionModelDto>)await _sut.GetUserActiveSubscriptions();

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserActivePayments_Success_ReturnsPayment()
    {
        var payment = new UserSubscriptionPayment { Id = 11 };
        _subscriptionService.Setup(s => s.GetUserActivePaymentsAsync()).ReturnsAsync(Result.Ok(payment));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.GetUserActivePayments();

        response.IsSuccess.Should().BeTrue();
        response.Value!.Id.Should().Be(11);
    }

    [Fact]
    public async Task GetUserActivePayments_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.GetUserActivePaymentsAsync())
            .ReturnsAsync(Result.Fail<UserSubscriptionPayment>("none"));

        var response = (SerializableResult<UserSubscriptionPayment>)await _sut.GetUserActivePayments();

        response.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CancelPayment_Success_ReturnsOk()
    {
        _subscriptionService.Setup(s => s.CancelPayment(7)).ReturnsAsync(Result.Ok());

        var response = (SerializableResult)await _sut.CancelPayment(7);

        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelPayment_Fail_ReturnsFailedResult()
    {
        _subscriptionService.Setup(s => s.CancelPayment(7)).ReturnsAsync(Result.Fail("already cancelled"));

        var response = (SerializableResult)await _sut.CancelPayment(7);

        response.IsFailed.Should().BeTrue();
    }
}
