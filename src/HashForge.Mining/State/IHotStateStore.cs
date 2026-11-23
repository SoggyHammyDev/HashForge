namespace HashForge.Mining.State;

/// <summary>Short-term storage for in-memory state snapshots.</summary>
public interface IHotStateStore
{
    Task SaveAsync(string key, string state, CancellationToken cancellationToken);

    Task<string?> LoadAsync(string key, CancellationToken cancellationToken);
}
