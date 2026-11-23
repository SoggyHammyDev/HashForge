using HashForge.Bitcoin.Notifications;
using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Jobs;
using HashForge.Mining.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace HashForge.Mining.UnitTests.Jobs;

public sealed class TemplateRefreshTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IChainTipSource _tips = Substitute.For<IChainTipSource>();
    private readonly IBlockTemplateSource _templates = Substitute.For<IBlockTemplateSource>();
    private readonly ITemplateSink _sink = Substitute.For<ITemplateSink>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly NetworkStatsStore _networkStats = new();
    private readonly ILogger<NodeReadiness> _readinessLogger = Substitute.For<ILogger<NodeReadiness>>();
    private readonly NodeReadiness _readiness;
    private readonly TemplateRefresher _refresher;
    private readonly ChainTipPoller _poller;

    public TemplateRefreshTests()
    {
        _readinessLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        SetTip(TestData.PreviousBlockHash);
        NodeIs(chain: "main", downloading: false);
        _readiness = new NodeReadiness(_networkStats, _readinessLogger);
        _refresher = new TemplateRefresher(_templates, _sink, _readiness, _time, NullLogger<TemplateRefresher>.Instance);
        _poller = new ChainTipPoller(
            _tips, _refresher, _readiness, _time, TestData.Options(o => { o.TemplateRefreshSeconds = 30; o.ChainTipPollSeconds = 2; }));
    }

    private void NodeIs(string chain, bool downloading) =>
        _networkStats.Update(new NetworkStats(chain, 1, 1, 1, 1, "00", downloading, 1, true, 0, TestData.Now));

    private void SetTip(string hash)
    {
        _tips.GetBestBlockHashAsync(Arg.Any<CancellationToken>()).Returns(hash);
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>()).Returns(TestData.Template(previousBlockHash: hash));
    }

    private int LogCount(LogLevel level) => _readinessLogger.ReceivedCalls()
        .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == level);

    private BlockNotificationListener Listener(IBlockNotificationSource source) =>
        new(source, _refresher, _readiness, _time, NullLogger<BlockNotificationListener>.Instance);

    private static IBlockNotificationSource Source(params string[] hashes)
    {
        var source = Substitute.For<IBlockNotificationSource>();
        source.IsEnabled.Returns(true);
        source.ListenAsync(Arg.Any<CancellationToken>()).Returns(call => Feed(hashes, call.Arg<CancellationToken>()));
        return source;
    }

    private static async IAsyncEnumerable<string> Feed(
        string[] hashes, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var hash in hashes)
        {
            yield return hash;
        }

        await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await Task.Delay(5, deadline.Token);
        }
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_FirstRun_THEN_TemplateIsPublishedAndStatusRecorded()
    {
        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == TestData.PreviousBlockHash));
        Assert.Equal(
            new TemplateStatus(TestData.PreviousBlockHash, TestData.Now) { RewardSatoshis = 312_500_000 },
            _refresher.Status);
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_TipUnchangedAndTemplateFresh_THEN_NothingIsPublished()
    {
        await _poller.ExecuteAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();
        _time.Advance(TimeSpan.FromSeconds(29));

        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_TipChanges_THEN_NewTemplateIsPublishedImmediately()
    {
        await _poller.ExecuteAsync(CancellationToken.None);
        var newTip = new string('2', 64);
        SetTip(newTip);

        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == newTip));
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_TemplateIsStale_THEN_TemplateIsRefreshed()
    {
        await _poller.ExecuteAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();
        _time.Advance(TimeSpan.FromSeconds(30));

        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_AnotherSourceAlreadyRefreshedForThisTip_THEN_PollerDoesNotRepeatIt()
    {
        await _refresher.RefreshAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();

        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_NodeIsStillDownloadingChain_THEN_NodeIsNotEvenAskedUntilItCatchesUp()
    {
        NodeIs(chain: "test4", downloading: true);

        await _poller.ExecuteAsync(CancellationToken.None);
        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
        await _tips.DidNotReceiveWithAnyArgs().GetBestBlockHashAsync(default);

        NodeIs(chain: "test4", downloading: false);
        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_PollerExecuteAsync_WHEN_ChainIsRegtest_THEN_WorkIsPublishedEvenIfNodeReportsDownloading()
    {
        NodeIs(chain: NetworkStats.RegtestChain, downloading: true);

        await _poller.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public void GIVEN_Poller_WHEN_Configured_THEN_UsesChainTipPollSetting()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), _poller.Interval);
        Assert.Equal("chain-tip-poller", _poller.Name);
    }

    [Fact]
    public void GIVEN_ReadinessIsReady_WHEN_NodeStatusUnknownOrDownloading_THEN_NotReadyAndReasonLoggedOnce()
    {
        var unknown = new NodeReadiness(new NetworkStatsStore(), _readinessLogger);

        Assert.False(unknown.IsReady());
        Assert.False(unknown.IsReady());
        Assert.False(unknown.IsReady());

        Assert.Equal(1, LogCount(LogLevel.Warning));
    }

    [Fact]
    public void GIVEN_ReportReady_WHEN_MiningWasPaused_THEN_ResumptionIsLoggedOnceAndNextPauseLogsAgain()
    {
        _readiness.ReportNotReady("node busy");
        _readiness.ReportReady();
        _readiness.ReportReady();
        _readiness.ReportNotReady("node busy again");

        Assert.Equal(2, LogCount(LogLevel.Warning));
        Assert.Equal(1, LogCount(LogLevel.Information));
        Assert.True(_readiness.IsReady());
    }

    [Theory]
    [InlineData(-9)]
    [InlineData(-10)]
    public async Task GIVEN_RefreshAsync_WHEN_NodeSaysItIsNotReady_THEN_NoWorkIsPublishedUntilItRecovers(int errorCode)
    {
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>())
            .Returns<BlockTemplate>(_ => throw new BitcoinRpcException(errorCode, "Bitcoin Core is in initial sync"));

        await _refresher.RefreshAsync(CancellationToken.None);
        await _refresher.RefreshAsync(CancellationToken.None);
        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
        Assert.Null(_refresher.Status.Tip);

        SetTip(TestData.PreviousBlockHash);
        await _refresher.RefreshAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_RefreshAsync_WHEN_NodeFailsForAnotherReason_THEN_ErrorPropagates()
    {
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new BitcoinRpcException(-1, "boom"));

        await Assert.ThrowsAsync<BitcoinRpcException>(() => _refresher.RefreshAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_RefreshAsync_WHEN_CalledConcurrently_THEN_TemplatesAreFetchedAndPublishedStrictlyOneAtATime()
    {
        var inFlight = 0;
        var overlapped = false;
        var height = 0;
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            overlapped |= Interlocked.Increment(ref inFlight) > 1;
            await Task.Delay(2);
            var template = TestData.Template() with { Height = Interlocked.Increment(ref height) };
            Interlocked.Decrement(ref inFlight);
            return template;
        });
        var published = new List<int>();
        _sink.When(s => s.Publish(Arg.Any<BlockTemplate>())).Do(call => published.Add(call.Arg<BlockTemplate>().Height));

        await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(() => _refresher.RefreshAsync(CancellationToken.None))));

        Assert.False(overlapped);
        Assert.Equal(Enumerable.Range(1, 25), published);
    }

    [Fact]
    public async Task GIVEN_Listener_WHEN_NodeAnnouncesBlock_THEN_NewWorkIsFetchedAtOnceWithoutWaitingForThePoller()
    {
        var newTip = new string('5', 64);
        SetTip(newTip);
        using var listener = Listener(Source(newTip));

        await listener.StartAsync(CancellationToken.None);
        await WaitForAsync(() => _refresher.Status.Tip == newTip);
        await listener.StopAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == newTip));
        await _tips.DidNotReceiveWithAnyArgs().GetBestBlockHashAsync(default);
    }

    [Fact]
    public async Task GIVEN_Listener_WHEN_NodeIsStillDownloadingChain_THEN_AnnouncementsAreIgnored()
    {
        NodeIs(chain: "main", downloading: true);
        var source = Source("aa", "bb", "cc");
        using var listener = Listener(source);

        await listener.StartAsync(CancellationToken.None);
        await WaitForAsync(() => source.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IBlockNotificationSource.ListenAsync)));
        await Task.Delay(50);
        await listener.StopAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public async Task GIVEN_Listener_WHEN_NoPushAddressConfigured_THEN_ItDoesNothingAndPollingCarriesOn()
    {
        var source = Substitute.For<IBlockNotificationSource>();
        source.IsEnabled.Returns(false);
        using var listener = Listener(source);

        await listener.StartAsync(CancellationToken.None);
        await listener.StopAsync(CancellationToken.None);

        source.DidNotReceiveWithAnyArgs().ListenAsync(default);
    }

    [Fact]
    public async Task GIVEN_Listener_WHEN_FeedBreaks_THEN_ItReconnectsAndKeepsDeliveringBlocks()
    {
        var newTip = new string('6', 64);
        SetTip(newTip);
        var attempts = 0;
        var source = Substitute.For<IBlockNotificationSource>();
        source.IsEnabled.Returns(true);
        source.ListenAsync(Arg.Any<CancellationToken>()).Returns(call =>
            Interlocked.Increment(ref attempts) == 1 ? throw new InvalidOperationException("socket died") : Feed([newTip], call.Arg<CancellationToken>()));
        using var listener = Listener(source);

        await listener.StartAsync(CancellationToken.None);
        await WaitForAsync(() =>
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            return _refresher.Status.Tip == newTip;
        });
        await listener.StopAsync(CancellationToken.None);

        Assert.True(attempts >= 2);
    }

    [Fact]
    public async Task GIVEN_Listener_WHEN_FetchingWorkFailsForOneBlock_THEN_LaterBlocksAreStillHandled()
    {
        var newTip = new string('7', 64);
        var calls = 0;
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            Interlocked.Increment(ref calls) == 1
                ? Task.FromException<BlockTemplate>(new HttpRequestException("node hiccup"))
                : Task.FromResult(TestData.Template(previousBlockHash: newTip)));
        using var listener = Listener(Source("first", newTip));

        await listener.StartAsync(CancellationToken.None);
        await WaitForAsync(() => _refresher.Status.Tip == newTip);
        await listener.StopAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == newTip));
    }
}