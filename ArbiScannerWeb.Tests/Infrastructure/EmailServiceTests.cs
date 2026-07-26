using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class EmailServiceTests
{
    private readonly Mock<ILogger<EmailService>> _logger = new();

    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "127.0.0.1",
            ["EmailSettings:SmtpPort"] = "1",
            ["EmailSettings:SenderEmail"] = "sender@example.com",
            ["EmailSettings:SenderPassword"] = "pw",
            ["EmailSettings:SenderName"] = "Sender"
        };
        if (overrides is not null)
            foreach (var (key, value) in overrides)
                defaults[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(defaults).Build();
    }

    [Fact]
    public async Task SendEmailAsync_MissingSmtpServer_ReturnsFail()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["EmailSettings:SmtpServer"] = "" });
        var sut = new EmailService(config, _logger.Object);

        var result = await sut.SendEmailAsync("to@example.com", "subject", "body");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("not properly configured");
    }

    [Fact]
    public async Task SendEmailAsync_MissingSenderEmail_ReturnsFail()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["EmailSettings:SenderEmail"] = "" });
        var sut = new EmailService(config, _logger.Object);

        var result = await sut.SendEmailAsync("to@example.com", "subject", "body");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailAsync_MissingSenderPassword_ReturnsFail()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["EmailSettings:SenderPassword"] = "" });
        var sut = new EmailService(config, _logger.Object);

        var result = await sut.SendEmailAsync("to@example.com", "subject", "body");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailAsync_SmtpUnreachable_ReturnsFail()
    {
        var config = BuildConfig();
        var sut = new EmailService(config, _logger.Object);

        var result = await sut.SendEmailAsync("to@example.com", "subject", "body");

        result.IsFailed.Should().BeTrue();
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
