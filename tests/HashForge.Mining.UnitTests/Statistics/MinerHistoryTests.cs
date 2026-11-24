using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Pools;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Statistics;

public sealed class MinerHistoryTests
{
    private const string Alice = "bc1qalice";
    private const string Bob = "bc1qbob";

    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly IPoolRegistry _registry = Substitute.For<IPoolRegistry>();
    private readonly IStatisticsReader _statistics = Substitute.For<IStatisticsReader>();
    private readonly MinerHistoryStore _store = new(TestData.Options(o => o.MinerHistoryHours = 1));

    public MinerHistoryTests() => _registry.Pools.Returns([TestData.Pool("pplns", PayoutScheme.Pplns, 3334)]);

    private MinerHistorySampler Sampler() =>
        new(_registry, _statistics, _store, _time, TestData.Options(o => o.HistorySampleMinutes = 7));

    private static StatsSnapshot Stats(double hashrate) =>
        new(1, 0, new Dictionary<ShareOutcome, long>(), 0, 0, hashrate, null, null);

    private static MinerSnapshot Miner(string address, double hashrate, params double[] workerHashrates) =>
        new(address, Stats(hashrate), [.. workerHashrates.Select((h, i) => new WorkerSnapshot($"w{i}", Stats(h)))]);

    private static MinerHistorySample Sample(int minutesAgo, double hashrate = 1) =>
        new(TestData.Now.AddMinutes(-minutesAgo).ToUnixTimeSeconds(), hashrate, 1);

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_MinersAreHashing_THEN_OneRoundedReadingIsStoredPerMiner()
    {
        _statistics.GetMiners("pplns").Returns([Miner(Alice, 4.8e12 + 0.4, 2.4e12, 2.4e12, 0), Miner(Bob, 1e12, 1e12)]);

        await Sampler().ExecuteAsync(CancellationToken.None);

        Assert.Equal([new MinerHistorySample(TestData.Now.ToUnixTimeSeconds(), 4.8e12, 2)], _store.Get("pplns", Alice));
        Assert.Equal(TestData.Now, Assert.Single(_store.Get("pplns", Bob)).Timestamp);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_MinerIsNotHashing_THEN_NothingIsStoredForThem()
    {
        _statistics.GetMiners("pplns").Returns([Miner(Alice, 0)]);

        await Sampler().ExecuteAsync(CancellationToken.None);

        Assert.Empty(_store.Get("pplns", Alice));
        Assert.Empty(_store.Export());
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_ReadingsAreOlderThanRetention_THEN_TheyAreDroppedAndIdleMinersForgotten()
    {
        _store.Add("pplns", Alice, Sample(minutesAgo: 90));
        _store.Add("pplns", Alice, Sample(minutesAgo: 30));
        _store.Add("pplns", Bob, Sample(minutesAgo: 61));
        _statistics.GetMiners("pplns").Returns([]);

        await Sampler().ExecuteAsync(CancellationToken.None);

        Assert.Equal([Sample(minutesAgo: 30)], _store.Get("pplns", Alice));
        Assert.DoesNotContain(_store.Export().Keys, k => k.Contains(Bob));
    }

    [Fact]
    public void GIVEN_Sampler_WHEN_Constructed_THEN_UsesHistoryIntervalAndName()
    {
        Assert.Equal(TimeSpan.FromMinutes(7), Sampler().Interval);
        Assert.Equal("miner-history", Sampler().Name);
    }

    [Fact]
    public void GIVEN_Get_WHEN_PoolNameCaseDiffersOrMinerIsOnAnotherPool_THEN_HistoryIsPerPoolAndCaseInsensitive()
    {
        _store.Add("PPLNS", Alice, Sample(5));

        Assert.Single(_store.Get("pplns", Alice));
        Assert.Empty(_store.Get("solo", Alice));
        Assert.Empty(_store.Get("pplns", Bob));
    }

    [Fact]
    public void GIVEN_Get_WHEN_StoreChangesAfterwards_THEN_ReturnedListIsUnaffected()
    {
        _store.Add("pplns", Alice, Sample(5));
        var before = _store.Get("pplns", Alice);

        _store.Add("pplns", Alice, Sample(0));

        Assert.Single(before);
        Assert.Equal(2, _store.Get("pplns", Alice).Count);
    }

    [Fact]
    public void GIVEN_ExportState_WHEN_ImportedIntoAnotherStore_THEN_HistoryIsRestoredInCompactForm()
    {
        _store.Add("pplns", Alice, Sample(5, hashrate: 4.8e12));
        _store.Add("pplns", Alice, Sample(0, hashrate: 4.9e12));
        var saved = new MinerHistoryState(_store).ExportState();
        var restored = new MinerHistoryStore(TestData.Options());
        var state = new MinerHistoryState(restored);

        state.ImportState(saved);

        Assert.Equal(_store.Get("pplns", Alice), restored.Get("pplns", Alice));
        Assert.Equal("miner-history", state.StateKey);
        Assert.Contains("\"t\":", saved);
        Assert.DoesNotContain("Timestamp", saved);
    }

    [Fact]
    public void GIVEN_ImportState_WHEN_StateIsJsonNull_THEN_HistoryIsEmpty()
    {
        _store.Add("pplns", Alice, Sample(5));

        new MinerHistoryState(_store).ImportState("null");

        Assert.Empty(_store.Get("pplns", Alice));
    }
}