namespace HashForge.Bitcoin.Rpc;

public interface IChainInfoSource
{
    Task<ChainInfo> GetChainInfoAsync(CancellationToken cancellationToken);

    Task<double> GetNetworkHashrateAsync(CancellationToken cancellationToken);
}
