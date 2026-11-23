using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Jobs;
using HashForge.Mining.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Jobs;

public sealed class TemplateMonitorTests
{
    private readonly IChainTipSource _tips = Substitute.For<IChainTipSource>();
    private readonly IBlockTemplateSource _templates = Substitute.For<IBlockTemplateSource>();
    private readonly ITemplateSink _sink = Substitute.For<ITemplateSink>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly NetworkStatsStore _networkStats = new();
    private readonly TemplateMonitor _monitor;

    public TemplateMonitorTests()
    {
        SetTip(TestData.PreviousBlockHash);
        NodeIs(chain: "main", downloading: false);
        _monitor = new TemplateMonitor(
            _tips,
            _templates,
            _sink,
            _networkStats,
            _time,
            TestData.Options(o => { o.TemplateRefreshSeconds = 30; o.ChainTipPollSeconds = 2; }),
            NullLogger<TemplateMonitor>.Instance);
    }

    private void NodeIs(string chain, bool downloading) =>
        _networkStats.Update(new NetworkStats(chain, 1, 1, 1, 1, "00", downloading, 1, true, 0, TestData.Now));

    private void SetTip(string hash)
    {
        _tips.GetBestBlockHashAsync(Arg.Any<CancellationToken>()).Returns(hash);
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>()).Returns(TestData.Template(previousBlockHash: hash));
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_FirstRun_THEN_TemplateIsPublished()
    {
        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == TestData.PreviousBlockHash));
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_TipUnchangedAndTemplateFresh_THEN_NothingIsPublished()
    {
        await _monitor.ExecuteAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();
        _time.Advance(TimeSpan.FromSeconds(29));

        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_TipChanges_THEN_NewTemplateIsPublishedImmediately()
    {
        await _monitor.ExecuteAsync(CancellationToken.None);
        var newTip = new string('2', 64);
        SetTip(newTip);

        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.Received(1).Publish(Arg.Is<BlockTemplate>(t => t.PreviousBlockHash == newTip));
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_TemplateIsStale_THEN_TemplateIsRefreshed()
    {
        await _monitor.ExecuteAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();
        _time.Advance(TimeSpan.FromSeconds(30));

        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_RefreshAsync_WHEN_CalledDirectly_THEN_PublishesAndResetsStaleTimer()
    {
        await _monitor.RefreshAsync(CancellationToken.None);
        _sink.ClearReceivedCalls();

        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Theory]
    [InlineData(-9)]
    [InlineData(-10)]
    public async Task GIVEN_RefreshAsync_WHEN_NodeIsNotReady_THEN_NoWorkIsPublishedUntilItRecovers(int errorCode)
    {
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>())
            .Returns<BlockTemplate>(_ => throw new BitcoinRpcException(errorCode, "Bitcoin Core is in initial sync"));

        await _monitor.ExecuteAsync(CancellationToken.None);
        await _monitor.ExecuteAsync(CancellationToken.None);
        _sink.DidNotReceiveWithAnyArgs().Publish(default!);

        SetTip(TestData.PreviousBlockHash);
        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeIsStillDownloadingChain_THEN_NoWorkIsPublishedUntilItCatchesUp()
    {
        NodeIs(chain: "test4", downloading: true);

        await _monitor.ExecuteAsync(CancellationToken.None);
        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
        await _tips.DidNotReceiveWithAnyArgs().GetBestBlockHashAsync(default);

        NodeIs(chain: "test4", downloading: false);
        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeStatusIsNotKnownYet_THEN_NoWorkIsPublished()
    {
        var monitor = new TemplateMonitor(
            _tips, _templates, _sink, new NetworkStatsStore(), _time, TestData.Options(), NullLogger<TemplateMonitor>.Instance);

        await monitor.ExecuteAsync(CancellationToken.None);

        _sink.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_ChainIsRegtest_THEN_WorkIsPublishedEvenIfNodeReportsDownloading()
    {
        NodeIs(chain: NetworkStats.RegtestChain, downloading: true);

        await _monitor.ExecuteAsync(CancellationToken.None);

        _sink.ReceivedWithAnyArgs(1).Publish(default!);
    }

    [Fact]
    public async Task GIVEN_RefreshAsync_WHEN_NodeFailsForAnotherReason_THEN_ErrorPropagates()
    {
        _templates.GetBlockTemplateAsync(Arg.Any<CancellationToken>())
            .Returns<BlockTemplate>(_ => throw new BitcoinRpcException(-1, "boom"));

        await Assert.ThrowsAsync<BitcoinRpcException>(() => _monitor.RefreshAsync(CancellationToken.None));
    }

    [Fact]
    public void GIVEN_Interval_WHEN_Configured_THEN_UsesChainTipPollSetting()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), _monitor.Interval);
        Assert.Equal("template-monitor", _monitor.Name);
    }
}
