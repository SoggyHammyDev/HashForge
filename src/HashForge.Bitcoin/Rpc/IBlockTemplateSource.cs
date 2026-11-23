namespace HashForge.Bitcoin.Rpc;

public interface IBlockTemplateSource
{
    Task<BlockTemplate> GetBlockTemplateAsync(CancellationToken cancellationToken);
}
