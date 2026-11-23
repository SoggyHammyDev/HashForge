using HashForge.Bitcoin.Primitives;

namespace HashForge.Bitcoin.UnitTests.Primitives;

public sealed class EncodingTests
{
    [Theory]
    [InlineData(0, "00")]
    [InlineData(252, "fc")]
    [InlineData(253, "fdfd00")]
    [InlineData(65535, "fdffff")]
    [InlineData(65536, "fe00000100")]
    public void GIVEN_WriteVarInt_WHEN_ValueCrossesSizeBoundaries_THEN_UsesCompactSizeEncoding(int value, string expectedHex)
    {
        var bytes = new BitcoinWriter().WriteVarInt(value).ToArray();

        Assert.Equal(expectedHex, Convert.ToHexStringLower(bytes));
    }

    [Fact]
    public void GIVEN_WriteVarInt_WHEN_ValueIsNegative_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitcoinWriter().WriteVarInt(-1));
    }

    [Fact]
    public void GIVEN_WriteUInt32_WHEN_Called_THEN_WritesLittleEndian()
    {
        Assert.Equal("04030201", Convert.ToHexStringLower(new BitcoinWriter().WriteUInt32(0x01020304).ToArray()));
    }

    [Fact]
    public void GIVEN_WriteInt64_WHEN_Called_THEN_WritesLittleEndian()
    {
        Assert.Equal("00f2052a01000000", Convert.ToHexStringLower(new BitcoinWriter().WriteInt64(5_000_000_000).ToArray()));
    }

    [Fact]
    public void GIVEN_WriteVarBytes_WHEN_Called_THEN_PrefixesLength()
    {
        var bytes = new BitcoinWriter().WriteByte(0xaa).WriteVarBytes([0x01, 0x02]).ToArray();

        Assert.Equal("aa020102", Convert.ToHexStringLower(bytes));
    }

    [Theory]
    [InlineData(0, "00")]
    [InlineData(1, "51")]
    [InlineData(16, "60")]
    [InlineData(17, "0111")]
    [InlineData(127, "017f")]
    [InlineData(128, "028000")]
    [InlineData(800000, "0300350c")]
    [InlineData(8388608, "0400008000")]
    public void GIVEN_EncodeHeight_WHEN_GivenHeight_THEN_MatchesBitcoinCoreEncoding(int height, string expectedHex)
    {
        Assert.Equal(expectedHex, Convert.ToHexStringLower(ScriptEncoding.EncodeHeight(height)));
    }

    [Fact]
    public void GIVEN_EncodeHeight_WHEN_HeightIsNegative_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScriptEncoding.EncodeHeight(-1));
    }

    [Fact]
    public void GIVEN_PushData_WHEN_DataFitsDirectPush_THEN_PrefixesLength()
    {
        Assert.Equal(new byte[] { 2, 0x48, 0x46 }, ScriptEncoding.PushData([0x48, 0x46]));
    }

    [Fact]
    public void GIVEN_PushData_WHEN_DataIsTooLong_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScriptEncoding.PushData(new byte[ScriptEncoding.MaxDirectPushLength + 1]));
    }
}
