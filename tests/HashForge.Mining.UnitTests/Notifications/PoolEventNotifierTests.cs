using HashForge.Mining.Network;
using HashForge.Mining.Notifications;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.UnitTests.Notifications;

public sealed class PoolEventNotifierTests
{
    private readonly INotificationOutbox _outbox = Substitute.For<INotificationOutbox>();
    private readonly NetworkStatsStore _networkStats = new();
    private readonly NotificationOptions _options = new();

    public PoolEventNotifierTests() =>
        _networkStats.Update(new NetworkStats("main", 1, 1, 1_000_000, 0, "00", false, 1, true, 0, TestData.Now));

    private PoolEventNotifier Notifier() => new(_outbox, TestData.DifficultyFrom(_networkStats), Options.Create(_options));

    [Fact]
    public void GIVEN_OnPoolBestShare_WHEN_NotificationsAreOn_THEN_BestShareIsQueuedWithWorkerAndNetworkDifficulty()
    {
        Notifier().OnPoolBestShare(TestData.Share(actual: 900));

        _outbox.Received(1).Enqueue(new BestShareNotification(
            "pplns", new BestShare(900, TestData.Worker.FullName, TestData.Now), 1_000_000));
    }

    [Fact]
    public void GIVEN_OnPoolBestShare_WHEN_NetworkDifficultyUnknown_THEN_QueuedWithZeroDifficulty()
    {
        new PoolEventNotifier(_outbox, TestData.DifficultyFrom(new NetworkStatsStore()), Options.Create(_options)).OnPoolBestShare(TestData.Share());

        _outbox.Received(1).Enqueue(Arg.Is<BestShareNotification>(n => n.NetworkDifficulty == 0));
    }

    [Fact]
    public void GIVEN_OnPoolBestShare_WHEN_OptionIsOff_THEN_NothingIsQueued()
    {
        _options.NotifyBestShare = false;

        Notifier().OnPoolBestShare(TestData.Share());

        _outbox.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public void GIVEN_OnPoolBestShare_WHEN_RecordsFallWithinCooldown_THEN_OnlyFirstIsNotifiedUntilCooldownPasses()
    {
        _options.BestShareCooldownSeconds = 60;
        var notifier = Notifier();

        notifier.OnPoolBestShare(TestData.Share(actual: 600));
        notifier.OnPoolBestShare(TestData.Share(actual: 700, timestamp: TestData.Now.AddSeconds(59)));
        _outbox.ReceivedWithAnyArgs(1).Enqueue(default!);

        notifier.OnPoolBestShare(TestData.Share(actual: 800, timestamp: TestData.Now.AddSeconds(60)));
        _outbox.ReceivedWithAnyArgs(2).Enqueue(default!);
    }

    [Fact]
    public void GIVEN_OnPoolBestShare_WHEN_DifferentPoolsSetRecords_THEN_EachPoolHasItsOwnCooldown()
    {
        var notifier = Notifier();

        notifier.OnPoolBestShare(TestData.Share("solo"));
        notifier.OnPoolBestShare(TestData.Share("pplns"));

        _outbox.ReceivedWithAnyArgs(2).Enqueue(default!);
    }

    [Fact]
    public async Task GIVEN_OnBlockFoundAsync_WHEN_OptionIsOn_THEN_BlockNotificationIsQueued()
    {
        var block = TestData.Block();

        await Notifier().OnBlockFoundAsync(block, CancellationToken.None);

        _outbox.Received(1).Enqueue(new BlockFoundNotification(block));
    }

    [Fact]
    public async Task GIVEN_OnBlockFoundAsync_WHEN_OptionIsOff_THEN_NothingIsQueued()
    {
        _options.NotifyBlockFound = false;

        await Notifier().OnBlockFoundAsync(TestData.Block(), CancellationToken.None);

        _outbox.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }
}
