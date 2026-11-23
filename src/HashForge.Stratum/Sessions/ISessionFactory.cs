using HashForge.Domain;
using HashForge.Stratum.Transport;

namespace HashForge.Stratum.Sessions;

public interface ISessionFactory
{
    StratumSession Create(PoolDefinition pool, IStratumConnection connection);
}
