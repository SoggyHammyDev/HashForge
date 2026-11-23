namespace HashForge.Bitcoin.Rpc;

public interface IChainTipSource
{
    Task<string> GetBestBlockHashAsync(CancellationToken cancellationToken);
}
