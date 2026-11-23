namespace HashForge.Mining.Network;

public interface INetworkStatsStore
{
    /// <summary>The latest stats, or null until the node has been reached for the first time.</summary>
    NetworkStats? Current { get; }

    void Update(NetworkStats stats);
}
