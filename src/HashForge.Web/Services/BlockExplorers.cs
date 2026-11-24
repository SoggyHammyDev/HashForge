namespace HashForge.Web.Services;

/// <summary>Picks the block explorer that matches the network the node is on.</summary>
public static class BlockExplorers
{
    private const string Site = "mempool.space";

    // Keyed by the chain name Bitcoin Core reports. A regtest chain is private, so it has no explorer.
    private static readonly Dictionary<string, BlockExplorer> ByChain = new(StringComparer.Ordinal)
    {
        ["main"] = new(Site, new Uri($"https://{Site}/block/")),
        ["testnet4"] = new($"{Site} (testnet4)", new Uri($"https://{Site}/testnet4/block/")),
        ["test"] = new($"{Site} (testnet3)", new Uri($"https://{Site}/testnet/block/")),
        ["signet"] = new($"{Site} (signet)", new Uri($"https://{Site}/signet/block/")),
    };

    /// <summary>The explorer for a chain, or null when the chain is unknown or has no public explorer.</summary>
    public static BlockExplorer? ForChain(string? chain) =>
        chain is not null && ByChain.TryGetValue(chain, out var explorer) ? explorer : null;
}
