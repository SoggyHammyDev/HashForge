using HashForge.Domain;

namespace HashForge.Mining.Payouts;

public interface IPayoutSchemeFactory
{
    IPayoutScheme Create(PoolDefinition pool);
}
