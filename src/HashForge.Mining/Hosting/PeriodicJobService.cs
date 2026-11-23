using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Hosting;

/// <summary>Runs every registered <see cref="IPeriodicJob"/> on its own timer and keeps it alive through failures.</summary>
public sealed partial class PeriodicJobService(
    IEnumerable<IPeriodicJob> jobs,
    TimeProvider timeProvider,
    ILogger<PeriodicJobService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(jobs.Select(job => RunAsync(job, stoppingToken)));

    private async Task RunAsync(IPeriodicJob job, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(job.Interval, timeProvider);
        try
        {
            do
            {
                await ExecuteSafelyAsync(job, stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task ExecuteSafelyAsync(IPeriodicJob job, CancellationToken stoppingToken)
    {
        try
        {
            await job.ExecuteAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A failing job must not stop the others; it is logged and retried on the next tick.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogJobFailed(exception, job.Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Background job {Job} failed and will be retried")]
    private partial void LogJobFailed(Exception exception, string job);
}
