using HashForge.Web.Services;

namespace HashForge.Web.UnitTests;

public sealed class BlockExplorersTests
{
    private const string Hash = "0000000000093b34b66752dc46bcadc74d7566bf7b1ba8d552db99c17bd12db8";

    [Theory]
    [InlineData("main", "mempool.space", "https://mempool.space/block/" + Hash)]
    [InlineData("testnet4", "mempool.space (testnet4)", "https://mempool.space/testnet4/block/" + Hash)]
    [InlineData("test", "mempool.space (testnet3)", "https://mempool.space/testnet/block/" + Hash)]
    [InlineData("signet", "mempool.space (signet)", "https://mempool.space/signet/block/" + Hash)]
    public void GIVEN_ForChain_WHEN_ChainIsPublic_THEN_ReturnsExplorerNamedForThatNetwork(string chain, string expectedName, string expectedAddress)
    {
        var explorer = BlockExplorers.ForChain(chain);

        Assert.NotNull(explorer);
        Assert.Equal(expectedName, explorer.Name);
        Assert.Equal(expectedAddress, explorer.BlockUri(Hash).AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("regtest")]
    [InlineData("MAIN")]
    public void GIVEN_ForChain_WHEN_ChainIsPrivateOrUnknown_THEN_ReturnsNull(string? chain)
    {
        Assert.Null(BlockExplorers.ForChain(chain));
    }
}
