namespace HashForge.Bitcoin.Rpc;

public interface IBlockHeaderSource
{
    /// <summary>The node's view of a block, or null when the node has never heard of it.</summary>
    Task<BlockHeaderInfo?> FindBlockHeaderAsync(string hash, CancellationToken cancellationToken);
}
