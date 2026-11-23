using HashForge.Bitcoin.Primitives;

namespace HashForge.Bitcoin.UnitTests.Primitives;

public sealed class HashingTests
{
    internal const string GenesisCoinbaseHex =
        "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff4d04ffff001d0104455468652054696d65732030332f4a616e2f32303039204368616e63656c6c6f72206f6e206272696e6b206f66207365636f6e64206261696c6f757420666f722062616e6b73ffffffff0100f2052a01000000434104678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5fac00000000";

    internal const string GenesisMerkleRootDisplayHex = "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";
    internal const string GenesisHashDisplayHex = "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f";

    [Fact]
    public void GIVEN_Hash_WHEN_DataIsGenesisCoinbase_THEN_ReturnsGenesisMerkleRoot()
    {
        var hash = Sha256d.Hash(Convert.FromHexString(GenesisCoinbaseHex));

        Assert.Equal(GenesisMerkleRootDisplayHex, HashHex.ToDisplayHex(hash));
    }

    [Fact]
    public void GIVEN_HashPair_WHEN_GivenTwoHalves_THEN_MatchesHashOfConcatenation()
    {
        var left = Enumerable.Repeat((byte)1, 32).ToArray();
        var right = Enumerable.Repeat((byte)2, 32).ToArray();

        Assert.Equal(Sha256d.Hash([.. left, .. right]), Sha256d.HashPair(left, right));
    }

    [Fact]
    public void GIVEN_FromDisplayHex_WHEN_Called_THEN_BytesAreReversed()
    {
        Assert.Equal(new byte[] { 0x03, 0x02, 0x01 }, HashHex.FromDisplayHex("010203"));
    }

    [Fact]
    public void GIVEN_ToDisplayHex_WHEN_Called_THEN_RoundTripsFromDisplayHexWithoutMutatingInput()
    {
        var bytes = HashHex.FromDisplayHex(GenesisHashDisplayHex);
        var copy = bytes.ToArray();

        Assert.Equal(GenesisHashDisplayHex, HashHex.ToDisplayHex(bytes));
        Assert.Equal(copy, bytes);
    }

    [Fact]
    public void GIVEN_ToStratumPreviousHash_WHEN_Called_THEN_WordOrderIsReversed()
    {
        const string display = "0000000011111111222222223333333344444444555555556666666677777777";

        Assert.Equal(
            "7777777766666666555555554444444433333333222222221111111100000000",
            HashHex.ToStratumPreviousHash(display));
    }
}
