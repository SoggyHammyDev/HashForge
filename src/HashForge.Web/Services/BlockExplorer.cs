namespace HashForge.Web.Services;

/// <summary>A public website where anyone can look up a block on one Bitcoin network.</summary>
/// <param name="Name">What the link is called, e.g. "mempool.space (testnet4)".</param>
/// <param name="BlockBase">The address a block hash is appended to. It ends with a slash.</param>
public sealed record BlockExplorer(string Name, Uri BlockBase)
{
    public Uri BlockUri(string hash) => new(BlockBase, hash);
}
