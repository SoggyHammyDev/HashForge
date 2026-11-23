using HashForge.Domain;

namespace HashForge.Persistence.Database;

public sealed class PoolDefinitionEntity
{
    public required string Name { get; set; }

    public PayoutScheme Scheme { get; set; }

    public int Port { get; set; }

    public decimal FeePercent { get; set; }

    public required string FeeAddress { get; set; }

    public double PplnsWindowMultiplier { get; set; }

    public long FinderBonusSatoshis { get; set; }

    public int MaxCoinbaseOutputs { get; set; }

    public long MinimumPayoutSatoshis { get; set; }
}
