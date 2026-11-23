using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Network;
using HashForge.Mining.Pools;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Statistics;

public sealed class StatisticsTests
{
    private const double NetworkDifficulty = 1000;

    private static readonly WorkerIdentity AliceRig1 = new("alice", "rig1");
    private static readonly WorkerIdentity AliceRig2 = new("alice", "rig2");
    private static readonly WorkerIdentity BobRig1 = new("bob", "rig1");

    private readonly NetworkStatsStore _networkStats = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly PoolDefinition _pplns = TestData.Pool("pplns", PayoutScheme.Pplns, 1);
    private readonly PoolDefinition _solo = TestData.Pool("solo", PayoutScheme.Solo, 2);
    private readonly StatisticsRig _service;

    public StatisticsTests()
    {
        _networkStats.Update(new NetworkStats("main", 1, 1, NetworkDifficulty, 0, "00", false, 1, true, 0, TestData.Now));
        _service = NewService();
    }

    private readonly IBestShareListener _bestShareListener = Substitute.For<IBestShareListener>();

    private StatisticsRig NewService() => new(_networkStats, _time, [_bestShareListener], TestData.Options());

    private void RecordOnBothPools()
    {
        foreach (var pool in new[] { "pplns", "solo" })
        {
            _service.OnShare(TestData.Share(pool, AliceRig1, credited: 100, actual: 150));
            _service.OnShare(TestData.Share(pool, AliceRig2, credited: 100, actual: 900));
            _service.OnShare(TestData.Share(pool, BobRig1, credited: 300, actual: 301));
            _service.OnShare(TestData.Share(pool, BobRig1, ShareOutcome.Duplicate));
        }
    }

    [Fact]
    public void GIVEN_GetPool_WHEN_NoSharesYet_THEN_ReturnsEmptySnapshotWithPoolDetails()
    {
        var pool = _service.GetPool(_pplns);

        Assert.Equal(("pplns", PayoutScheme.Pplns, 1, 0, 0), (pool.Name, pool.Scheme, pool.Port, pool.MinerCount, pool.WorkerCount));
        Assert.Equal(0, pool.Stats.Accepted);
    }

    [Fact]
    public void GIVEN_OnShare_WHEN_SharesArrive_THEN_PoolTotalsEffortAndBestShareAreTracked()
    {
        RecordOnBothPools();

        var pool = _service.GetPool(_pplns);

        Assert.Equal(3, pool.Stats.Accepted);
        Assert.Equal(1, pool.Stats.Rejected);
        Assert.Equal(50, pool.Stats.EffortPercent);
        Assert.Equal("alice.rig2", pool.Stats.BestShare!.Worker);
        Assert.Equal(2, pool.MinerCount);
        Assert.Equal(3, pool.WorkerCount);
    }

    [Fact]
    public void GIVEN_OnShare_WHEN_ShareIsPoolsBestSoFar_THEN_ListenersAreToldOnceForThatShareOnly()
    {
        var first = TestData.Share("pplns", AliceRig1, actual: 150);
        var lower = TestData.Share("pplns", AliceRig1, actual: 100);
        var higher = TestData.Share("pplns", BobRig1, actual: 900);
        var rejected = TestData.Share("pplns", BobRig1, ShareOutcome.Duplicate, actual: 1e12);

        foreach (var share in new[] { first, lower, higher, rejected })
        {
            _service.OnShare(share);
        }

        _bestShareListener.Received(1).OnPoolBestShare(first);
        _bestShareListener.Received(1).OnPoolBestShare(higher);
        _bestShareListener.ReceivedWithAnyArgs(2).OnPoolBestShare(default!);
    }

    [Fact]
    public void GIVEN_GetPool_WHEN_MinersHaveGoneQuiet_THEN_OnlyRecentlyActiveMinersAndWorkersAreCounted()
    {
        RecordOnBothPools();
        _time.Advance(TimeSpan.FromMinutes(30));
        _service.OnShare(TestData.Share("pplns", AliceRig1, timestamp: _time.GetUtcNow()));

        var pool = _service.GetPool(_pplns);

        Assert.Equal((1, 1), (pool.MinerCount, pool.WorkerCount));
        Assert.Equal(2, _service.GetMiners("pplns").Count);
    }

    [Fact]
    public void GIVEN_GetMiner_WHEN_MinerHasWorkers_THEN_ReturnsMinerAndPerWorkerStatsSortedByName()
    {
        RecordOnBothPools();

        var alice = _service.GetMiner("pplns", "alice")!;

        Assert.Equal(2, alice.Stats.Accepted);
        Assert.Equal(20, alice.Stats.EffortPercent);
        Assert.Equal(["rig1", "rig2"], alice.Workers.Select(w => w.Name));
        Assert.Equal(900, alice.Workers[1].Stats.BestShare!.Difficulty);
        Assert.Equal(1, _service.GetMiner("pplns", "bob")!.Workers[0].Stats.Rejected);
    }

    [Fact]
    public void GIVEN_GetMiner_WHEN_PoolOrMinerUnknown_THEN_ReturnsNull()
    {
        RecordOnBothPools();

        Assert.Null(_service.GetMiner("missing", "alice"));
        Assert.Null(_service.GetMiner("pplns", "nobody"));
    }

    [Fact]
    public void GIVEN_GetMiners_WHEN_Called_THEN_OrderedByHashrateDescending()
    {
        RecordOnBothPools();

        Assert.Equal(["bob", "alice"], _service.GetMiners("pplns").Select(m => m.Address));
        Assert.Empty(_service.GetMiners("missing"));
    }

    [Fact]
    public void GIVEN_OnShare_WHEN_NetworkDifficultyUnknown_THEN_EffortIsZero()
    {
        var service = new StatisticsRig(new NetworkStatsStore(), _time, [], TestData.Options());
        service.OnShare(TestData.Share("pplns"));

        Assert.Equal(0, service.GetPool(_pplns).Stats.EffortPercent);
    }

    [Fact]
    public void GIVEN_GetRoundEffortPercent_WHEN_PoolIsShared_THEN_ReturnsPoolEffort()
    {
        RecordOnBothPools();

        Assert.Equal(50, _service.GetRoundEffortPercent(_pplns, "alice"));
    }

    [Fact]
    public void GIVEN_GetRoundEffortPercent_WHEN_PoolIsSolo_THEN_ReturnsThatMinersEffort()
    {
        RecordOnBothPools();

        Assert.Equal(20, _service.GetRoundEffortPercent(_solo, "alice"));
        Assert.Equal(0, _service.GetRoundEffortPercent(_solo, "nobody"));
    }

    [Fact]
    public async Task GIVEN_OnBlockFoundAsync_WHEN_PoolIsShared_THEN_EveryMinersRoundResets()
    {
        RecordOnBothPools();

        await _service.OnBlockFoundAsync(TestData.Block("pplns", PayoutScheme.Pplns, AliceRig1), CancellationToken.None);

        Assert.Equal(0, _service.GetPool(_pplns).Stats.EffortPercent);
        Assert.Equal(0, _service.GetMiner("pplns", "alice")!.Stats.EffortPercent);
        Assert.Equal(0, _service.GetMiner("pplns", "bob")!.Workers[0].Stats.EffortPercent);
        Assert.Equal(3, _service.GetPool(_pplns).Stats.Accepted);
        Assert.Equal(50, _service.GetPool(_solo).Stats.EffortPercent);
    }

    [Fact]
    public async Task GIVEN_OnBlockFoundAsync_WHEN_PoolIsSolo_THEN_OnlyFindersRoundResets()
    {
        RecordOnBothPools();

        await _service.OnBlockFoundAsync(TestData.Block("solo", PayoutScheme.Solo, AliceRig1), CancellationToken.None);

        var alice = _service.GetMiner("solo", "alice")!;
        Assert.Equal(0, alice.Stats.EffortPercent);
        Assert.All(alice.Workers, w => Assert.Equal(0, w.Stats.EffortPercent));
        Assert.Equal(30, _service.GetMiner("solo", "bob")!.Stats.EffortPercent);
    }

    [Fact]
    public async Task GIVEN_OnBlockFoundAsync_WHEN_PoolIsUnknown_THEN_NothingHappens()
    {
        RecordOnBothPools();

        await _service.OnBlockFoundAsync(TestData.Block("missing"), CancellationToken.None);

        Assert.Equal(50, _service.GetPool(_pplns).Stats.EffortPercent);
    }

    [Fact]
    public void GIVEN_ImportState_WHEN_GivenExportedState_THEN_PoolMinerAndWorkerStatsAreRestored()
    {
        RecordOnBothPools();
        var restored = NewService();

        restored.ImportState(_service.ExportState());

        Assert.Equal(_service.GetPool(_pplns).Stats.Accepted, restored.GetPool(_pplns).Stats.Accepted);
        Assert.Equal(50, restored.GetPool(_solo).Stats.EffortPercent);
        Assert.Equal("alice.rig2", restored.GetPool(_pplns).Stats.BestShare!.Worker);
        var bob = restored.GetMiner("pplns", "bob")!;
        Assert.Equal(1, bob.Stats.Rejected);
        Assert.Equal(1, bob.Workers[0].Stats.RejectedByReason[ShareOutcome.Duplicate]);
        Assert.Equal("statistics", restored.StateKey);
    }

    [Fact]
    public async Task GIVEN_PoolHistorySampler_WHEN_PoolsExist_THEN_OneSamplePerPoolIsStored()
    {
        RecordOnBothPools();
        var registry = new PoolRegistry();
        registry.TryAdd(_pplns, out _);
        registry.TryAdd(_solo, out _);
        var repository = Substitute.For<IPoolHistoryRepository>();
        IReadOnlyList<PoolHistorySample>? stored = null;
        await repository.AddAsync(Arg.Do<IReadOnlyList<PoolHistorySample>>(s => stored = s), Arg.Any<CancellationToken>());
        var sampler = new PoolHistorySampler(registry, _service.Reader, repository, _time, TestData.Options(o => o.HistorySampleMinutes = 3));

        await sampler.ExecuteAsync(CancellationToken.None);

        var expectedHashrate = _service.GetPool(_pplns).Stats.Hashrate;
        Assert.Equal(
            [new PoolHistorySample("pplns", TestData.Now, expectedHashrate, 2, 3), new PoolHistorySample("solo", TestData.Now, expectedHashrate, 2, 3)],
            stored);
        Assert.Equal(TimeSpan.FromMinutes(3), sampler.Interval);
        Assert.Equal("pool-history", sampler.Name);
    }

    [Fact]
    public async Task GIVEN_PoolHistorySampler_WHEN_NoPoolsExist_THEN_NothingIsStored()
    {
        var repository = Substitute.For<IPoolHistoryRepository>();
        var sampler = new PoolHistorySampler(new PoolRegistry(), _service.Reader, repository, _time, TestData.Options());

        await sampler.ExecuteAsync(CancellationToken.None);

        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
