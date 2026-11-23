using HashForge.Domain;

namespace HashForge.Mining.Payouts;

public sealed class PayoutSchemeFactory(TimeProvider timeProvider) : IPayoutSchemeFactory
{
    public IPayoutScheme Create(PoolDefinition pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return pool.Scheme switch
        {
            PayoutScheme.Solo => new SoloPayoutScheme(),
            PayoutScheme.Prop => new ProportionalPayoutScheme(timeProvider),
            PayoutScheme.Pplns => new PplnsPayoutScheme(timeProvider, pool.PplnsWindowMultiplier),
            PayoutScheme.FinderBonus => new FinderBonusPayoutScheme(new ProportionalPayoutScheme(timeProvider), pool.FinderBonusSatoshis),
            _ => throw new ArgumentOutOfRangeException(nameof(pool), pool.Scheme, "Unknown payout scheme."),
        };
    }
}