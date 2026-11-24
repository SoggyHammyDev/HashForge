using HashForge.Domain;

namespace HashForge.Persistence.Database;

public sealed class BlockEntity
{
    public long Id { get; set; }

    public required string PoolName { get; set; }

    public PayoutScheme Scheme { get; set; }

    public int Height { get; set; }

    public required string Hash { get; set; }

    public required string WorkerAddress { get; set; }

    public required string WorkerName { get; set; }

    public long RewardSatoshis { get; set; }

    public double EffortPercent { get; set; }

    public DateTimeOffset FoundAt { get; set; }

    public bool Accepted { get; set; }

    public string? RejectReason { get; set; }

    public int MinersPaid { get; set; }

    public int Confirmations { get; set; }

    public bool Orphaned { get; set; }

    public ICollection<BlockPayoutEntity> Payouts { get; init; } = [];
}
