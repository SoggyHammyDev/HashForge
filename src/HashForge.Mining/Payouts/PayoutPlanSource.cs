using HashForge.Domain;

namespace HashForge.Mining.Payouts;

/// <summary>Answers "who gets paid for a block on this pool?" from the pool's payout scheme.</summary>
public sealed class PayoutPlanSource(IPayoutSchemeRegistry schemes) : IPayoutPlanSource
{
    public PayoutPlan GetPlan(PoolDefinition pool, string minerAddress) => schemes.SchemeFor(pool).GetPlan(minerAddress);

    public bool IsMinerSpecific(PoolDefinition pool) => schemes.SchemeFor(pool).IsMinerSpecific;
}