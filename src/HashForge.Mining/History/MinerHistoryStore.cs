using Microsoft.Extensions.Options;

namespace HashForge.Mining.History;

/// <summary>Holds each miner's recent hashrate readings in memory. Nothing here is written to the database.</summary>
public sealed class MinerHistoryStore(IOptions<MiningOptions> options) : IMinerHistory
{
    private const char KeySeparator = '|';

    private readonly TimeSpan _retention = TimeSpan.FromHours(options.Value.MinerHistoryHours);
    private readonly Lock _gate = new();
    private Dictionary<string, List<MinerHistorySample>> _series = new(StringComparer.Ordinal);

    public IReadOnlyList<MinerHistorySample> Get(string poolName, string address)
    {
        lock (_gate)
        {
            return _series.TryGetValue(Key(poolName, address), out var samples) ? [.. samples] : [];
        }
    }

    public void Add(string poolName, string address, MinerHistorySample sample)
    {
        lock (_gate)
        {
            var key = Key(poolName, address);
            if (!_series.TryGetValue(key, out var samples))
            {
                _series[key] = samples = [];
            }

            samples.Add(sample);
        }
    }

    /// <summary>Drops readings older than the retention period, and miners left with none.</summary>
    public void Prune(DateTimeOffset now)
    {
        var cutoff = (now - _retention).ToUnixTimeSeconds();
        lock (_gate)
        {
            foreach (var (key, samples) in _series.ToList())
            {
                samples.RemoveAll(s => s.UnixSeconds < cutoff);
                if (samples.Count == 0)
                {
                    _series.Remove(key);
                }
            }
        }
    }

    public IReadOnlyDictionary<string, MinerHistorySample[]> Export()
    {
        lock (_gate)
        {
            return _series.ToDictionary(s => s.Key, s => s.Value.ToArray(), StringComparer.Ordinal);
        }
    }

    public void Import(IReadOnlyDictionary<string, MinerHistorySample[]> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        lock (_gate)
        {
            _series = series.ToDictionary(s => s.Key, s => s.Value.ToList(), StringComparer.Ordinal);
        }
    }

    /// <summary>Pool names are not case-sensitive anywhere else, so they are not here either.</summary>
    private static string Key(string poolName, string address) =>
        string.Concat(poolName.ToUpperInvariant(), KeySeparator.ToString(), address);
}