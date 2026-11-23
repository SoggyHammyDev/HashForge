namespace HashForge.Mining.Jobs;

public interface ITemplateRefresher
{
    /// <summary>Fetches a fresh block template now, without waiting for the next poll.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}
