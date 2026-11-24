using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Network;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Network;

public sealed class NetworkStatsTests
{
    private static NetworkStats Stats(bool initialBlockDownload = false, int height = 100, int headers = 100) =>
        new("main", height, headers, 1, 2, "00", initialBlockDownload, 1, true, 5, TestData.Now);

    [Fact]
    public void GIVEN_Current_WHEN_NothingStoredYet_THEN_IsNull()
    {
        Assert.Null(new NetworkStatsStore().Current);
    }

    [Fact]
    public void GIVEN_Update_WHEN_Called_THEN_CurrentReturnsLatest()
    {
        var store = new NetworkStatsStore();
        var latest = Stats();

        store.Update(Stats(height: 1));
        store.Update(latest);

        Assert.Same(latest, store.Current);
    }

    [Theory]
    [InlineData(false, 100, 100, true)]
    [InlineData(true, 100, 100, false)]
    [InlineData(false, 99, 100, false)]
    public void GIVEN_IsSynced_WHEN_NodeStateVaries_THEN_ReflectsDownloadAndHeaderGap(bool ibd, int height, int headers, bool expected)
    {
        Assert.Equal(expected, Stats(ibd, height, headers).IsSynced);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeResponds_THEN_StoreHoldsCombinedStats()
    {
        var source = Substitute.For<IChainInfoSource>();
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(new ChainInfo
        {
            Chain = "main", Blocks = 850_000, Headers = 850_001, BestBlockHash = "00ab", Difficulty = 9e13,
            InitialBlockDownload = true, VerificationProgress = 0.5, Pruned = true, SizeOnDisk = 123, Time = 1_700_000_000,
        });
        source.GetNetworkHashrateAsync(Arg.Any<CancellationToken>()).Returns(6e20);
        var store = new NetworkStatsStore();
        var job = new NetworkStatsJob(source, store, new FakeTimeProvider(TestData.Now), TestData.Options(o => o.NetworkStatsRefreshSeconds = 45));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            new NetworkStats("main", 850_000, 850_001, 9e13, 6e20, "00ab", true, 0.5, true, 123, TestData.Now)
            {
                LastBlockAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            },
            store.Current);
        Assert.Equal(TimeSpan.FromSeconds(45), job.Interval);
        Assert.Equal("network-stats", job.Name);
    }

    private static ChainInfo Tip(string hash, long time) => new()
    {
        Chain = "testnet4", Blocks = 10, Headers = 10, BestBlockHash = hash, Difficulty = 1, Time = time,
    };

    private static (NetworkStatsJob Job, NetworkStatsStore Store, IChainInfoSource Source, FakeTimeProvider Time) Rig()
    {
        var source = Substitute.For<IChainInfoSource>();
        var store = new NetworkStatsStore();
        var time = new FakeTimeProvider(TestData.Now);
        return (new NetworkStatsJob(source, store, time, TestData.Options()), store, source, time);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_FirstTipIsStampedInTheFuture_THEN_LastBlockIsNeverLaterThanNow()
    {
        var (job, store, source, _) = Rig();
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(Tip("aa", TestData.Now.AddHours(2).ToUnixTimeSeconds()));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(TestData.Now, store.Current!.LastBlockAt);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NewBlockArrivesWhileWatching_THEN_ItIsDatedByWhenItWasSeen()
    {
        var (job, store, source, time) = Rig();
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(Tip("aa", TestData.Now.AddHours(-1).ToUnixTimeSeconds()));
        await job.ExecuteAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(5));
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(Tip("bb", TestData.Now.AddHours(2).ToUnixTimeSeconds()));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(TestData.Now.AddMinutes(5), store.Current!.LastBlockAt);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_TipIsUnchanged_THEN_LastBlockTimeIsKeptSoItsAgeGrows()
    {
        var (job, store, source, time) = Rig();
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(Tip("aa", TestData.Now.AddHours(2).ToUnixTimeSeconds()));
        await job.ExecuteAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(5));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(TestData.Now, store.Current!.LastBlockAt);
        Assert.Equal(TestData.Now.AddMinutes(5), store.Current.UpdatedAt);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeReportsNoBlockTime_THEN_LastBlockIsUnknown()
    {
        var (job, store, source, _) = Rig();
        source.GetChainInfoAsync(Arg.Any<CancellationToken>()).Returns(Tip("aa", 0));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Null(store.Current!.LastBlockAt);
    }
}
