namespace HashForge.Domain.UnitTests;

public sealed class BlockHashFormatTests
{
    [Fact]
    public void GIVEN_IsValid_WHEN_TextIsSixtyFourLowercaseHexDigits_THEN_ReturnsTrue()
    {
        Assert.True(BlockHashFormat.IsValid("0000000000093b34b66752dc46bcadc74d7566bf7b1ba8d552db99c17bd12db8"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00ab")]
    [InlineData("0000000000093B34B66752DC46BCADC74D7566BF7B1BA8D552DB99C17BD12DB8")]
    [InlineData("0000000000093b34b66752dc46bcadc74d7566bf7b1ba8d552db99c17bd12db8ff")]
    [InlineData("g000000000093b34b66752dc46bcadc74d7566bf7b1ba8d552db99c17bd12db8")]
    [InlineData("<script>alert(1)</script>")]
    public void GIVEN_IsValid_WHEN_TextIsNotABlockHash_THEN_ReturnsFalse(string? hash)
    {
        Assert.False(BlockHashFormat.IsValid(hash));
    }
}
