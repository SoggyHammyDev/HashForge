using System.Net;
using HashForge.Mining.Jobs;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Stratum.Server;

/// <summary>Pushes fresh work to every connected miner whenever the jobs change.</summary>
public sealed partial class JobBroadcaster(
    IJobSource jobSource,
    ISessionRegistry sessions,
    IWorkSender workSender,
    ILogger<JobBroadcaster> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private Task _lastBroadcast = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobSource.JobsChanged += OnJobsChanged;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        jobSource.JobsChanged -= OnJobsChanged;
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _lastBroadcast.ConfigureAwait(false);
    }

    public void Dispose() => _stopping.Dispose();

    private void OnJobsChanged(object? sender, JobsChangedEventArgs args) =>
        _lastBroadcast = BroadcastAsync(args.IsNewBlock, _stopping.Token);

    private Task BroadcastAsync(bool cleanJobs, CancellationToken cancellationToken) =>
        Task.WhenAll(sessions.Sessions.Select(session => SendSafelyAsync(session, cleanJobs, cancellationToken)));

    private async Task SendSafelyAsync(StratumSession session, bool cleanJobs, CancellationToken cancellationToken)
    {
        try
        {
            await workSender.SendJobAsync(session, cleanJobs, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A miner that cannot be reached must not stop the others getting work.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogSendFailed(exception, session.RemoteAddress);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not send work to miner {RemoteAddress}")]
    private partial void LogSendFailed(Exception exception, IPAddress remoteAddress);
}
