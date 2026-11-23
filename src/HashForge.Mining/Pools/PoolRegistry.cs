using System.Diagnostics.CodeAnalysis;
using HashForge.Domain;

namespace HashForge.Mining.Pools;

public sealed class PoolRegistry : IPoolRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PoolDefinition> _pools = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<PoolChangedEventArgs>? PoolAdded;

    public event EventHandler<PoolChangedEventArgs>? PoolRemoved;

    public IReadOnlyList<PoolDefinition> Pools
    {
        get
        {
            lock (_gate)
            {
                return [.. _pools.Values.OrderBy(p => p.Port)];
            }
        }
    }

    public PoolDefinition? Find(string name)
    {
        lock (_gate)
        {
            return _pools.GetValueOrDefault(name);
        }
    }

    public bool TryAdd(PoolDefinition pool, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(pool);

        lock (_gate)
        {
            error = PoolDefinitionValidator.FirstError(pool) ?? FindConflict(pool);
            if (error is not null)
            {
                return false;
            }

            _pools.Add(pool.Name, pool);
        }

        PoolAdded?.Invoke(this, new PoolChangedEventArgs(pool));
        return true;
    }

    public bool Remove(string name)
    {
        PoolDefinition? removed;
        lock (_gate)
        {
            if (!_pools.Remove(name, out removed))
            {
                return false;
            }
        }

        PoolRemoved?.Invoke(this, new PoolChangedEventArgs(removed));
        return true;
    }

    private string? FindConflict(PoolDefinition pool)
    {
        if (_pools.ContainsKey(pool.Name))
        {
            return $"A pool named '{pool.Name}' already exists.";
        }

        var samePort = _pools.Values.FirstOrDefault(p => p.Port == pool.Port);
        return samePort is null ? null : $"Port {pool.Port} is already used by pool '{samePort.Name}'.";
    }
}
