using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class CompositeRealtimeNotifierTests
{
    [Fact]
    public async Task ForwardsTheSameGroupAndPayloadToEveryNotifier()
    {
        var first = new Mock<IRealtimeNotifier>();
        var second = new Mock<IRealtimeNotifier>();
        var payload = new object();
        var sut = new CompositeRealtimeNotifier([first.Object, second.Object], NullLogger<CompositeRealtimeNotifier>.Instance);

        await sut.NotifyGroupAsync("group", payload);

        first.Verify(n => n.NotifyGroupAsync("group", payload), Times.Once);
        second.Verify(n => n.NotifyGroupAsync("group", payload), Times.Once);
    }

    [Fact]
    public async Task AFailingNotifierDoesNotStopTheOthersOrThrow()
    {
        var failing = new Mock<IRealtimeNotifier>();
        failing.Setup(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<object>())).ThrowsAsync(new InvalidOperationException("boom"));
        var healthy = new Mock<IRealtimeNotifier>();
        var sut = new CompositeRealtimeNotifier([failing.Object, healthy.Object], NullLogger<CompositeRealtimeNotifier>.Instance);

        var act = () => sut.NotifyGroupAsync("group", new object());

        await act.Should().NotThrowAsync();
        healthy.Verify(n => n.NotifyGroupAsync("group", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task NoNotifiers_IsANoOp()
    {
        var sut = new CompositeRealtimeNotifier([], NullLogger<CompositeRealtimeNotifier>.Instance);

        await sut.Invoking(s => s.NotifyGroupAsync("group", new object())).Should().NotThrowAsync();
    }
}
