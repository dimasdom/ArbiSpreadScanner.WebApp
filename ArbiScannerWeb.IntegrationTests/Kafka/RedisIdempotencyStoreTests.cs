using ArbiScannerWeb.IntegrationTests.Support;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

public sealed class RedisIdempotencyStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis).Build();
    private ConnectionMultiplexer _connection = default!;
    private RedisIdempotencyStore _store = default!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _store = new RedisIdempotencyStore(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task First_caller_wins_the_claim_and_a_second_caller_sees_it_in_progress()
    {
        (await _store.TryBeginAsync("k1", CancellationToken.None)).Should().Be(ClaimResult.Started);
        (await _store.TryBeginAsync("k1", CancellationToken.None)).Should().Be(ClaimResult.InProgress);
    }

    [Fact]
    public async Task A_crash_before_completion_leaves_a_claim_that_is_not_mistaken_for_done()
    {
        await _store.TryBeginAsync("k2", CancellationToken.None);

        // No Complete: the process "crashed". The redelivered message must NOT be skipped as a duplicate.
        (await _store.TryBeginAsync("k2", CancellationToken.None)).Should().NotBe(ClaimResult.AlreadyDone);
        (await _connection.GetDatabase().KeyTimeToLiveAsync("k2")).Should().BeLessThanOrEqualTo(RedisIdempotencyStore.ProcessingTtl);
    }

    [Fact]
    public async Task Completed_work_is_reported_as_done_with_the_long_ttl()
    {
        await _store.TryBeginAsync("k3", CancellationToken.None);
        await _store.CompleteAsync("k3");

        (await _store.TryBeginAsync("k3", CancellationToken.None)).Should().Be(ClaimResult.AlreadyDone);
        (await _connection.GetDatabase().KeyTimeToLiveAsync("k3")).Should().BeGreaterThan(RedisIdempotencyStore.ProcessingTtl);
    }

    [Fact]
    public async Task An_abandoned_claim_can_be_taken_again_immediately()
    {
        await _store.TryBeginAsync("k4", CancellationToken.None);
        await _store.AbandonAsync("k4");

        (await _store.TryBeginAsync("k4", CancellationToken.None)).Should().Be(ClaimResult.Started);
    }
}
