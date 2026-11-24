using HashForge.Domain;
using HashForge.Mining.Network;
using HashForge.Mining.Shares;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.UnitTests.Statistics;

/// <summary>The statistics classes wired together over one store, as they are in the running pool.</summary>
internal sealed class StatisticsRig
{
    private readonly StatisticsRecorder _recorder;
    private readonly StatisticsReader _reader;
    private readonly StatisticsState _state;

    public StatisticsRig(
        INetworkStatsStore networkStats,
        TimeProvider timeProvider,
        IEnumerable<IBestShareListener> listeners,
        IOptions<MiningOptions> options)
    {
        var store = new StatisticsStore(options);
        _recorder = new StatisticsRecorder(store, listeners);
        _reader = new StatisticsReader(store, TestData.DifficultyFrom(networkStats), timeProvider);
        _state = new StatisticsState(store);
    }

    public IStatisticsReader Reader => _reader;

    public string StateKey => _state.StateKey;

    public void OnShare(ShareResult share) => _recorder.OnShare(share);

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken) => _recorder.OnBlockFoundAsync(block, cancellationToken);

    public PoolSnapshot GetPool(PoolDefinition pool) => _reader.GetPool(pool);

    public IReadOnlyList<MinerSnapshot> GetMiners(string poolName) => _reader.GetMiners(poolName);

    public MinerSnapshot? GetMiner(string poolName, string address) => _reader.GetMiner(poolName, address);

    public double GetRoundEffortPercent(PoolDefinition pool, string minerAddress) => _reader.GetRoundEffortPercent(pool, minerAddress);

    public string ExportState() => _state.ExportState();

    public void ImportState(string state) => _state.ImportState(state);
}