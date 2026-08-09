using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class AccountControllerTests
{
    private readonly Mock<IAccountService> _accountService = new();

    private AccountController CreateController(DefaultHttpContext? httpContext = null)
    {
        var ctrl = new AccountController(_accountService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext ?? new DefaultHttpContext()
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task GetUserData_ServiceSucceeds_ReturnsOk()
    {
        _accountService.Setup(s => s.GetUserData())
            .ReturnsAsync(Result.Ok(new AccountDto { Email = "a@b.com" }));

        var ctrl = CreateController();

        var result = await ctrl.GetUserData();
        var body = (SerializableResult<AccountDto>)result.Value!;

        body.IsSuccess.Should().BeTrue();
        body.Value!.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task GetUserData_ServiceFails_ReturnsFailedResult()
    {
        _accountService.Setup(s => s.GetUserData())
            .ReturnsAsync(Result.Fail<AccountDto>("not authenticated"));

        var ctrl = CreateController();

        var result = await ctrl.GetUserData();
        var body = (SerializableResult<AccountDto>)result.Value!;

        body.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDetails_DelegatesToServiceAndReturnsResult()
    {
        var dto = new AccountEditDto { SpreadSize = 3 };
        _accountService.Setup(s => s.UpdateDetails(dto))
            .ReturnsAsync(Result.Ok(dto));

        var ctrl = CreateController();

        var result = await ctrl.UpdateDetails(dto);
        var body = (SerializableResult<AccountEditDto>)result.Value!;

        body.IsSuccess.Should().BeTrue();
        _accountService.Verify(s => s.UpdateDetails(dto), Times.Once);
    }
}
