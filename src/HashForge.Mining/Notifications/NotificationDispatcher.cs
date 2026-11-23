using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Notifications;

/// <summary>Background service that delivers queued notifications to every channel.</summary>
public sealed partial class NotificationDispatcher(
    INotificationOutbox queue,
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                foreach (var channel in channels)
                {
                    await SendSafelyAsync(channel, notification, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task SendSafelyAsync(INotificationChannel channel, PoolNotification notification, CancellationToken stoppingToken)
    {
        try
        {
            await channel.SendAsync(notification, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A notification that cannot be delivered must never disturb mining; it is logged and dropped.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogSendFailed(exception, notification.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not deliver {Notification}")]
    private partial void LogSendFailed(Exception exception, string notification);
}
