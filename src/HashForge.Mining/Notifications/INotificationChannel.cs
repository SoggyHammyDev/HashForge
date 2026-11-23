namespace HashForge.Mining.Notifications;

/// <summary>A place notifications are delivered to. Add another destination by registering another implementation.</summary>
public interface INotificationChannel
{
    Task SendAsync(PoolNotification notification, CancellationToken cancellationToken);
}
