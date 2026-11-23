namespace HashForge.Mining.Jobs;

public interface ITemplateRefresher
{
    TemplateStatus Status { get; }

    /// <summary>Fetches a fresh block template now and publishes it. Calls are processed strictly one at a time.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}