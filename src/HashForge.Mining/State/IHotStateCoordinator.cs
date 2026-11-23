namespace HashForge.Mining.State;

public interface IHotStateCoordinator
{
    Task SaveAllAsync(CancellationToken cancellationToken);

    Task RestoreAllAsync(CancellationToken cancellationToken);
}
