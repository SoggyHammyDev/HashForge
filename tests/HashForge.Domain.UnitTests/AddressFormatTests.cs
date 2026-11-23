namespace HashForge.Domain.UnitTests;

public sealed class AddressFormatTests
{
    [Theory]
    [InlineData("bc1qar0srrr7xfkvy5l643lydnw9re59gtzzwf5mdq")]
    [InlineData("tb1qdlavq4cg7j0mnfsqtvv4mpkgu8mheclkpqc0n5")]
    [InlineData("bc1p5d7rjq7g6rdk2yhzks9smlaqtedr4dekq08ge8ztwac72sfr9rusxg3297")]
    [InlineData("1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2")]
    [InlineData("3J98t1WpEZ73CNmQviecrnyiWrnqRhWNLy")]
    public void GIVEN_IsPlausible_WHEN_TextHasAddressShape_THEN_ReturnsTrue(string address)
    {
        Assert.True(AddressFormat.IsPlausible(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("bc1q with spaces in the middle of it")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("bc1qaddress'; DROP TABLE blocks;--")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("bc1qaddress%00withnullbyte")]
    [InlineData("bc1qaddress\r\nInjected: header")]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("bc1qädresswithunicodeletters1234")]
    public void GIVEN_IsPlausible_WHEN_TextIsNotAddressShaped_THEN_ReturnsFalse(string? address)
    {
        Assert.False(AddressFormat.IsPlausible(address));
    }

    [Fact]
    public void GIVEN_IsPlausible_WHEN_LengthIsAtOrBeyondLimits_THEN_OnlyLengthsInRangePass()
    {
        Assert.True(AddressFormat.IsPlausible(new string('a', AddressFormat.MinLength)));
        Assert.True(AddressFormat.IsPlausible(new string('a', AddressFormat.MaxLength)));
        Assert.False(AddressFormat.IsPlausible(new string('a', AddressFormat.MinLength - 1)));
        Assert.False(AddressFormat.IsPlausible(new string('a', AddressFormat.MaxLength + 1)));
        Assert.False(AddressFormat.IsPlausible(new string('a', 100_000)));
    }
}
