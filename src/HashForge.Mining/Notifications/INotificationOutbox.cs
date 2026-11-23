namespace HashForge.Mining.Notifications;

/// <summary>Holds notifications between the mining path, which must never wait, and the slower senders.</summary>
public interface INotificationOutbox
{
    void Enqueue(PoolNotification notification);

    IAsyncEnumerable<PoolNotification> ReadAllAsync(CancellationToken cancellationToken);
}
