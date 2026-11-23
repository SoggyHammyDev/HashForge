using HashForge.Mining.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace HashForge.Mining.UnitTests.State;

public sealed class HotStateTests
{
    private readonly IHotStateStore _store = Substitute.For<IHotStateStore>();
    private readonly IStatePersistable _first = Participant("first", "state-1");
    private readonly IStatePersistable _second = Participant("second", "state-2");
    private readonly IHotStateCoordinator _coordinatorStub = Substitute.For<IHotStateCoordinator>();

    private static IStatePersistable Participant(string key, string state)
    {
        var participant = Substitute.For<IStatePersistable>();
        participant.StateKey.Returns(key);
        participant.ExportState().Returns(state);
        return participant;
    }

    private HotStateCoordinator Coordinator() => new([_first, _second], _store, NullLogger<HotStateCoordinator>.Instance);

    private HotStateLifecycleService Lifecycle() => new(_coordinatorStub, NullLogger<HotStateLifecycleService>.Instance);

    [Fact]
    public async Task GIVEN_SaveAllAsync_WHEN_Called_THEN_EveryParticipantIsSavedUnderItsKey()
    {
        await Coordinator().SaveAllAsync(CancellationToken.None);

        await _store.Received(1).SaveAsync("first", "state-1", Arg.Any<CancellationToken>());
        await _store.Received(1).SaveAsync("second", "state-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_RestoreAllAsync_WHEN_StoreHasStateForOneParticipant_THEN_OnlyThatOneIsRestored()
    {
        _store.LoadAsync("first", Arg.Any<CancellationToken>()).Returns("saved");
        _store.LoadAsync("second", Arg.Any<CancellationToken>()).Returns((string?)null);

        await Coordinator().RestoreAllAsync(CancellationToken.None);

        _first.Received(1).ImportState("saved");
        _second.DidNotReceiveWithAnyArgs().ImportState(default!);
    }

    [Fact]
    public async Task GIVEN_SnapshotJobExecuteAsync_WHEN_Called_THEN_SavesAllState()
    {
        var job = new HotStateSnapshotJob(_coordinatorStub, TestData.Options(o => o.StateSnapshotSeconds = 7));

        await job.ExecuteAsync(CancellationToken.None);

        await _coordinatorStub.Received(1).SaveAllAsync(Arg.Any<CancellationToken>());
        Assert.Equal(TimeSpan.FromSeconds(7), job.Interval);
        Assert.Equal("state-snapshot", job.Name);
    }

    [Fact]
    public async Task GIVEN_LifecycleStartAsync_WHEN_Called_THEN_StateIsRestored()
    {
        await Lifecycle().StartAsync(CancellationToken.None);

        await _coordinatorStub.Received(1).RestoreAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_LifecycleStopAsync_WHEN_Called_THEN_StateIsSaved()
    {
        await Lifecycle().StopAsync(CancellationToken.None);

        await _coordinatorStub.Received(1).SaveAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_LifecycleStartAsync_WHEN_StoreIsUnavailable_THEN_StartupStillSucceeds()
    {
        _coordinatorStub.RestoreAllAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("redis down"));

        await Lifecycle().StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_LifecycleStopAsync_WHEN_StoreIsUnavailable_THEN_ShutdownStillSucceeds()
    {
        _coordinatorStub.SaveAllAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("redis down"));

        await Lifecycle().StopAsync(CancellationToken.None);
    }
}
