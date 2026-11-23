namespace HashForge.Stratum.Transport;

public interface IConnectionListener : IAsyncDisposable
{
    int Port { get; }

    Task<IStratumConnection> AcceptAsync(CancellationToken cancellationToken);
}
