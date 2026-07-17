using ArbiScannerWeb.Abstractions.Interfaces;
using FluentResults;

namespace ArbiScannerWeb.IntegrationTests.Support;

internal sealed class FakeEmailService : IEmailService
{
    public Task<Result> SendEmailAsync(string toEmail, string subject, string body)
        => Task.FromResult(Result.Ok());
}
