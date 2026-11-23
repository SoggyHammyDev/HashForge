namespace HashForge.Stratum.Transport;

public sealed class TcpConnectionListenerFactory(Microsoft.Extensions.Options.IOptions<StratumOptions> options) : IConnectionListenerFactory
{
    public IConnectionListener Create(int port) => new TcpConnectionListener(port, options.Value.MaxLineLength);
}
