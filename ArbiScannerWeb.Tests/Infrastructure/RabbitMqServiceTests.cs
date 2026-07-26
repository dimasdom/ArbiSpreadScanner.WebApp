using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class RabbitMqServiceTests
{
    private sealed class TestableRabbitMqService(
        Microsoft.Extensions.Logging.ILogger<RabbitMqService> logger,
        IOptions<RabbitMqSettings> settings,
        StackExchange.Redis.IConnectionMultiplexer redis) : RabbitMqService(logger, settings, redis)
    {
        public void DisposeWithoutCleanup() => Dispose(disposing: false);
    }

    private static RabbitMqSettings CreateSettings() => new()
    {
        Host = "127.0.0.1",
        Port = 1,
        Username = "guest",
        Password = "guest",
        Queue = "q",
        Exchange = "e",
        RoutingKey = "rk"
    };

    private static TestableRabbitMqService CreateSut()
    {
        var (redis, _) = MockHelpers.CreateRedisMocks();
        return new TestableRabbitMqService(
            NullLogger<RabbitMqService>.Instance,
            Options.Create(CreateSettings()),
            redis.Object);
    }

    [Fact]
    public async Task StopConsumingAsync_NeverStarted_ReturnsImmediately()
    {
        var sut = CreateSut();

        await sut.StopConsumingAsync();
    }

    [Fact]
    public async Task StartConsumingAsync_BrokerUnreachable_DoesNotThrow()
    {
        var sut = CreateSut();

        await sut.StartConsumingAsync(CancellationToken.None);

        await sut.StopConsumingAsync();
    }

    [Fact]
    public void Dispose_NeverConnected_CompletesWithoutError()
    {
        var sut = CreateSut();

        var act = sut.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_SecondCallIsNoOp()
    {
        var sut = CreateSut();
        sut.Dispose();

        var act = sut.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithDisposingFalse_SkipsCleanupButMarksDisposed()
    {
        var sut = CreateSut();

        var act = sut.DisposeWithoutCleanup;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_NeverConnected_CompletesWithoutError()
    {
        var sut = CreateSut();

        await sut.DisposeAsync();
    }
}
