using System.Threading.Channels;

namespace HashForge.Mining.Notifications;

public sealed class NotificationOutbox : INotificationOutbox
{
    private const int Capacity = 100;

    private readonly Channel<PoolNotification> _channel = Channel.CreateBounded<PoolNotification>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(PoolNotification notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<PoolNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
