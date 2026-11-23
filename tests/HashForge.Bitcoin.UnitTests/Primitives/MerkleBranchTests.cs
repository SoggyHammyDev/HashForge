using HashForge.Bitcoin.Primitives;

namespace HashForge.Bitcoin.UnitTests.Primitives;

public sealed class MerkleBranchTests
{
    private static byte[] Id(byte seed) => Sha256d.Hash([seed]);

    /// <summary>A straightforward full-tree implementation to check the branch shortcut against.</summary>
    private static byte[] FullTreeRoot(List<byte[]> level)
    {
        while (level.Count > 1)
        {
            if (level.Count % 2 != 0)
            {
                level.Add(level[^1]);
            }

            level = level.Chunk(2).Select(pair => Sha256d.HashPair(pair[0], pair[1])).ToList();
        }

        return level[0];
    }

    [Fact]
    public void GIVEN_Build_WHEN_BlockHasOnlyCoinbase_THEN_BranchIsEmptyAndRootIsCoinbaseId()
    {
        var branch = MerkleBranch.Build([]);

        Assert.Empty(branch);
        Assert.Equal(Id(0), MerkleBranch.ComputeRoot(Id(0), branch));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(13)]
    public void GIVEN_ComputeRoot_WHEN_BranchBuiltForAnyTransactionCount_THEN_MatchesFullTreeRoot(int otherTransactions)
    {
        var coinbase = Id(0);
        var others = Enumerable.Range(1, otherTransactions).Select(i => Id((byte)i)).ToList();

        var root = MerkleBranch.ComputeRoot(coinbase, MerkleBranch.Build(others));

        Assert.Equal(FullTreeRoot([coinbase, .. others]), root);
    }

    [Fact]
    public void GIVEN_Build_WHEN_OneOtherTransaction_THEN_BranchIsThatTransaction()
    {
        var branch = MerkleBranch.Build([Id(1)]);

        Assert.Equal([Id(1)], branch);
    }
}
