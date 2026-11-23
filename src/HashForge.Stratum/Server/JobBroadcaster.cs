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
        _lastBroadcast = BroadcastAsync(_stopping.Token);

    /// <summary>
    /// Every miner is sent work at once. Broadcasts may overlap when blocks arrive in quick succession;
    /// each session keeps its own jobs in order, so an older broadcast can never overwrite a newer one.
    /// </summary>
    private Task BroadcastAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(sessions.Sessions.Select(session => SendSafelyAsync(session, cancellationToken)));

    private async Task SendSafelyAsync(StratumSession session, CancellationToken cancellationToken)
    {
        try
        {
            await workSender.SendJobAsync(session, cancellationToken).ConfigureAwait(false);
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
