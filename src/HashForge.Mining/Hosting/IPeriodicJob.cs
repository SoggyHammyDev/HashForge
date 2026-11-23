namespace HashForge.Mining.Hosting;

/// <summary>A unit of work that runs repeatedly in the background.</summary>
public interface IPeriodicJob
{
    string Name { get; }

    TimeSpan Interval { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
