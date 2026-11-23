namespace HashForge.Persistence.Database;

public sealed class BlockPayoutEntity
{
    public long Id { get; set; }

    public long BlockId { get; set; }

    public required string Address { get; set; }

    public long Satoshis { get; set; }
}
