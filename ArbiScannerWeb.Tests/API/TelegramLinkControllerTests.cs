using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentAssertions;
using FluentResults;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class TelegramLinkControllerTests
{
    private readonly Mock<IUserSettingsService> _userSettingsService = new();
    private readonly TelegramLinkController _sut;

    public TelegramLinkControllerTests()
    {
        _sut = new TelegramLinkController(_userSettingsService.Object);
    }

    [Fact]
    public async Task CreateTelegramLinkRequest_ServiceSucceeds_ReturnsSuccessResult()
    {
        var link = new TelegramLinkRequest { AccountId = "acc-1" };
        _userSettingsService.Setup(s => s.CreateLinkRequestAsyncForAuthUser())
            .ReturnsAsync(Result.Ok(link));

        var response = await _sut.CreateTelegramLinkRequest();
        var body = (SerializableResult<TelegramLinkRequest>)response.Value!;

        body.IsSuccess.Should().BeTrue();
        body.Value!.AccountId.Should().Be("acc-1");
    }

    [Fact]
    public async Task CreateTelegramLinkRequest_ServiceFails_ReturnsFailedResult()
    {
        _userSettingsService.Setup(s => s.CreateLinkRequestAsyncForAuthUser())
            .ReturnsAsync(Result.Fail<TelegramLinkRequest>("no user"));

        var response = await _sut.CreateTelegramLinkRequest();
        var body = (SerializableResult<TelegramLinkRequest>)response.Value!;

        body.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveTelegramLink_ServiceSucceeds_ReturnsSuccessResult()
    {
        _userSettingsService.Setup(s => s.RemoveTelegramLinkAsyncForAuthUser())
            .ReturnsAsync(Result.Ok());

        var response = await _sut.RemoveTelegramLink();
        var body = (SerializableResult)response.Value!;

        body.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveTelegramLink_ServiceFails_ReturnsFailedResult()
    {
        _userSettingsService.Setup(s => s.RemoveTelegramLinkAsyncForAuthUser())
            .ReturnsAsync(Result.Fail("not linked"));

        var response = await _sut.RemoveTelegramLink();
        var body = (SerializableResult)response.Value!;

        body.IsFailed.Should().BeTrue();
    }
}
