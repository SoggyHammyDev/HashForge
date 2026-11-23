using System.Diagnostics.CodeAnalysis;
using HashForge.Domain;

namespace HashForge.Mining.Pools;

/// <summary>The pools that are currently running. Pools can be added and removed while the service runs.</summary>
public interface IPoolRegistry
{
    event EventHandler<PoolChangedEventArgs>? PoolAdded;

    event EventHandler<PoolChangedEventArgs>? PoolRemoved;

    IReadOnlyList<PoolDefinition> Pools { get; }

    PoolDefinition? Find(string name);

    bool TryAdd(PoolDefinition pool, [NotNullWhen(false)] out string? error);

    bool Remove(string name);
}
