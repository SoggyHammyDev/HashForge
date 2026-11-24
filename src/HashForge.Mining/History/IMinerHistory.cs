namespace HashForge.Mining.History;

public interface IMinerHistory
{
    /// <summary>A miner's recent hashrate readings on one pool, oldest first; empty when there are none.</summary>
    IReadOnlyList<MinerHistorySample> Get(string poolName, string address);
}