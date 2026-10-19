using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Messaging;

public class IdempotentExecutorTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1);

    private sealed class FakeStore : IIdempotencyStore
    {
        private readonly Queue<ClaimResult> _script;

        public FakeStore(params ClaimResult[] script) => _script = new Queue<ClaimResult>(script);

        public List<string> Calls { get; } = [];

        public Task<ClaimResult> TryBeginAsync(string key, CancellationToken cancellationToken)
        {
            Calls.Add("begin");
            return Task.FromResult(_script.Dequeue());
        }

        public Task CompleteAsync(string key)
        {
            Calls.Add("complete");
            return Task.CompletedTask;
        }

        public Task AbandonAsync(string key)
        {
            Calls.Add("abandon");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Runs_the_work_and_marks_it_done_when_the_claim_is_won()
    {
        var store = new FakeStore(ClaimResult.Started);
        var runs = 0;

        var ran = await IdempotentExecutor.RunOnceAsync(store, "k", _ => { runs++; return Task.CompletedTask; }, TimeSpan.FromSeconds(1), PollInterval, CancellationToken.None);

        ran.Should().BeTrue();
        runs.Should().Be(1);
        store.Calls.Should().Equal("begin", "complete");
    }

    [Fact]
    public async Task Skips_the_work_for_an_already_processed_message()
    {
        var store = new FakeStore(ClaimResult.AlreadyDone);
        var runs = 0;

        var ran = await IdempotentExecutor.RunOnceAsync(store, "k", _ => { runs++; return Task.CompletedTask; }, TimeSpan.FromSeconds(1), PollInterval, CancellationToken.None);

        ran.Should().BeFalse();
        runs.Should().Be(0);
        store.Calls.Should().Equal("begin");
    }

    [Fact]
    public async Task Releases_the_claim_when_the_work_fails_so_a_retry_can_run_it_again()
    {
        var store = new FakeStore(ClaimResult.Started);

        var act = () => IdempotentExecutor.RunOnceAsync(store, "k", _ => throw new InvalidOperationException("boom"), TimeSpan.FromSeconds(1), PollInterval, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.Calls.Should().Equal("begin", "abandon");
    }

    [Fact]
    public async Task Waits_for_a_live_claim_then_takes_over_once_it_expires()
    {
        var store = new FakeStore(ClaimResult.InProgress, ClaimResult.InProgress, ClaimResult.Started);
        var runs = 0;

        var ran = await IdempotentExecutor.RunOnceAsync(store, "k", _ => { runs++; return Task.CompletedTask; }, TimeSpan.FromSeconds(30), PollInterval, CancellationToken.None);

        ran.Should().BeTrue();
        runs.Should().Be(1);
        store.Calls.Should().Equal("begin", "begin", "begin", "complete");
    }

    [Fact]
    public async Task Gives_up_with_a_timeout_when_the_claim_never_clears()
    {
        var store = new FakeStore(Enumerable.Repeat(ClaimResult.InProgress, 10_000).ToArray());

        var act = () => IdempotentExecutor.RunOnceAsync(store, "k", _ => Task.CompletedTask, TimeSpan.FromMilliseconds(30), PollInterval, CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
