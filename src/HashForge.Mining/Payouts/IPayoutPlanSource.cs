using HashForge.Domain;

namespace HashForge.Mining.Payouts;

public interface IPayoutPlanSource
{
    PayoutPlan GetPlan(PoolDefinition pool, string minerAddress);

    /// <summary>True when each miner on the pool currently needs a job of their own.</summary>
    bool IsMinerSpecific(PoolDefinition pool);
}
