namespace HashForge.Bitcoin.Notifications;

/// <summary>Pushes the hash of each new block the moment the node learns of it.</summary>
public interface IBlockNotificationSource
{
    /// <summary>False when no push channel is configured, in which case callers fall back to polling.</summary>
    bool IsEnabled { get; }

    /// <summary>Yields block hashes (display hex) as they are announced, until cancelled.</summary>
    IAsyncEnumerable<string> ListenAsync(CancellationToken cancellationToken);
}