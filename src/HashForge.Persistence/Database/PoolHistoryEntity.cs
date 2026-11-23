namespace HashForge.Persistence.Database;

public sealed class PoolHistoryEntity
{
    public long Id { get; set; }

    public required string PoolName { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public double Hashrate { get; set; }

    public int Miners { get; set; }

    public int Workers { get; set; }
}
