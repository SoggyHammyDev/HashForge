using HashForge.Bitcoin.Primitives;

namespace HashForge.Bitcoin.UnitTests.Primitives;

public sealed class CoinbaseAndBlockTests
{
    private const string ExpectedPrefixHex =
        "02000000" + "01" + "0000000000000000000000000000000000000000000000000000000000000000" + "ffffffff" +
        "10" + "0300350c" + "08";

    private const string ExpectedSuffixHex = "024846" + "ffffffff" + "01" + "00f2052a01000000" + "0151" + "00000000";

    private static readonly byte[] Extranonce = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] WitnessCommitment = [0x6a, 0x24, 0xaa, 0x21, 0xa9, 0xed];

    private static CoinbaseSpec Spec(byte[]? witnessCommitment = null, int tagLength = 2) => new(
        Height: 800_000,
        Tag: tagLength == 2 ? "HF"u8.ToArray() : new byte[tagLength],
        ExtranonceSize: 8,
        Outputs: [new TransactionOutput(5_000_000_000, [0x51])],
        WitnessCommitmentScript: witnessCommitment);

    [Fact]
    public void GIVEN_Build_WHEN_NoWitnessCommitment_THEN_PrefixAndSuffixMatchHandBuiltTransaction()
    {
        var coinbase = CoinbaseBuilder.Build(Spec());

        Assert.Equal(ExpectedPrefixHex, Convert.ToHexStringLower(coinbase.Prefix));
        Assert.Equal(ExpectedSuffixHex, Convert.ToHexStringLower(coinbase.Suffix));
        Assert.Equal(8, coinbase.ExtranonceSize);
    }

    [Fact]
    public void GIVEN_Build_WHEN_WitnessCommitmentPresent_THEN_ZeroValueCommitmentOutputIsAppended()
    {
        var coinbase = CoinbaseBuilder.Build(Spec(WitnessCommitment));

        var suffixHex = Convert.ToHexStringLower(coinbase.Suffix);
        Assert.EndsWith("02" + "00f2052a01000000" + "0151" + "0000000000000000" + "06" + "6a24aa21a9ed" + "00000000", suffixHex);
    }

    [Fact]
    public void GIVEN_Build_WHEN_TagIsEmpty_THEN_NoTagPushIsWritten()
    {
        var coinbase = CoinbaseBuilder.Build(Spec(tagLength: 0));

        Assert.StartsWith("ffffffff", Convert.ToHexStringLower(coinbase.Suffix));
    }

    [Fact]
    public void GIVEN_Build_WHEN_ScriptExceedsConsensusLimit_THEN_Throws()
    {
        Assert.Throws<ArgumentException>(() => CoinbaseBuilder.Build(Spec(tagLength: 75) with { ExtranonceSize = 30 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(76)]
    public void GIVEN_Build_WHEN_ExtranonceSizeIsOutOfRange_THEN_Throws(int extranonceSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CoinbaseBuilder.Build(Spec() with { ExtranonceSize = extranonceSize }));
    }

    [Fact]
    public void GIVEN_Assemble_WHEN_ExtranonceSupplied_THEN_ItSitsBetweenPrefixAndSuffix()
    {
        var coinbase = CoinbaseBuilder.Build(Spec());

        var hex = Convert.ToHexStringLower(coinbase.Assemble(Extranonce));

        Assert.Equal(ExpectedPrefixHex + "0102030405060708" + ExpectedSuffixHex, hex);
    }

    [Fact]
    public void GIVEN_Assemble_WHEN_ExtranonceHasWrongLength_THEN_Throws()
    {
        var coinbase = CoinbaseBuilder.Build(Spec());

        Assert.Throws<ArgumentException>(() => coinbase.Assemble([1, 2, 3]));
    }

    [Fact]
    public void GIVEN_ComputeTransactionId_WHEN_Called_THEN_IsDoubleHashOfLegacySerialisation()
    {
        var coinbase = CoinbaseBuilder.Build(Spec(WitnessCommitment));

        Assert.Equal(Sha256d.Hash(coinbase.Assemble(Extranonce)), coinbase.ComputeTransactionId(Extranonce));
    }

    [Fact]
    public void GIVEN_AssembleForBlock_WHEN_NoWitnessCommitment_THEN_ReturnsLegacySerialisation()
    {
        var coinbase = CoinbaseBuilder.Build(Spec());

        Assert.Equal(coinbase.Assemble(Extranonce), coinbase.AssembleForBlock(Extranonce));
    }

    [Fact]
    public void GIVEN_AssembleForBlock_WHEN_WitnessCommitmentPresent_THEN_AddsMarkerFlagAndReservedValue()
    {
        var coinbase = CoinbaseBuilder.Build(Spec(WitnessCommitment));
        var legacyHex = Convert.ToHexStringLower(coinbase.Assemble(Extranonce));

        var blockHex = Convert.ToHexStringLower(coinbase.AssembleForBlock(Extranonce));

        var body = legacyHex[8..^8];
        var witness = "01" + "20" + new string('0', 64);
        Assert.Equal("02000000" + "0001" + body + witness + "00000000", blockHex);
    }

    [Fact]
    public void GIVEN_SerializeHeader_WHEN_HeaderIsGenesisBlock_THEN_HashIsGenesisHash()
    {
        var header = new BlockHeader(
            Version: 1,
            PreviousBlockHash: new byte[32],
            MerkleRoot: HashHex.FromDisplayHex(HashingTests.GenesisMerkleRootDisplayHex),
            Time: 1231006505,
            Bits: 0x1d00ffff,
            Nonce: 2083236893);

        var serialized = BlockSerializer.SerializeHeader(header);

        Assert.Equal(BlockSerializer.HeaderSize, serialized.Length);
        Assert.Equal(HashingTests.GenesisHashDisplayHex, HashHex.ToDisplayHex(Sha256d.Hash(serialized)));
    }

    [Fact]
    public void GIVEN_SerializeBlock_WHEN_GivenTransactions_THEN_WritesHeaderCountCoinbaseThenOthers()
    {
        var block = BlockSerializer.SerializeBlock([0xaa], [0xbb], [[0xcc], [0xdd, 0xee]]);

        Assert.Equal("aa03bbccddee", Convert.ToHexStringLower(block));
    }
}
