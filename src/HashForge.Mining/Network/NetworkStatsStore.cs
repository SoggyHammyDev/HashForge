namespace HashForge.Mining.Network;

public sealed class NetworkStatsStore : INetworkStatsStore
{
    private volatile NetworkStats? _current;

    public NetworkStats? Current => _current;

    public void Update(NetworkStats stats) => _current = stats;
}
