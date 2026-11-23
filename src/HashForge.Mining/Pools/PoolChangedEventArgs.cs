using HashForge.Domain;

namespace HashForge.Mining.Pools;

public sealed class PoolChangedEventArgs(PoolDefinition pool) : EventArgs
{
    public PoolDefinition Pool { get; } = pool;
}
