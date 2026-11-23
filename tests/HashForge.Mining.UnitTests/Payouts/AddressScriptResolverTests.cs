using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Payouts;

namespace HashForge.Mining.UnitTests.Payouts;

public sealed class AddressScriptResolverTests
{
    private readonly IAddressValidator _validator = Substitute.For<IAddressValidator>();
    private readonly AddressScriptResolver _resolver;

    public AddressScriptResolverTests() => _resolver = new AddressScriptResolver(_validator);

    private void NodeSays(string address, AddressValidation validation) =>
        _validator.ValidateAddressAsync(address, Arg.Any<CancellationToken>()).Returns(validation);

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_NodeAcceptsAddress_THEN_ReturnsScript()
    {
        NodeSays("good", new AddressValidation { IsValid = true, ScriptPubKey = "0014ab" });

        var resolution = await _resolver.ResolveAsync("good", CancellationToken.None);

        Assert.True(resolution.IsValid);
        Assert.Equal(new byte[] { 0x00, 0x14, 0xab }, resolution.ScriptPubKey);
        Assert.Null(resolution.Error);
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_AddressWasAlreadyValidated_THEN_NodeIsAskedOnlyOnce()
    {
        NodeSays("good", new AddressValidation { IsValid = true, ScriptPubKey = "51" });

        await _resolver.ResolveAsync("good", CancellationToken.None);
        await _resolver.ResolveAsync("good", CancellationToken.None);

        await _validator.Received(1).ValidateAddressAsync("good", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_NodeRejectsAddressWithReason_THEN_ReturnsNodeReason()
    {
        NodeSays("bad", new AddressValidation { IsValid = false, Error = "Invalid checksum" });

        var resolution = await _resolver.ResolveAsync("bad", CancellationToken.None);

        Assert.False(resolution.IsValid);
        Assert.Null(resolution.ScriptPubKey);
        Assert.Equal("Invalid checksum", resolution.Error);
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_NodeRejectsAddressWithoutReason_THEN_ReturnsDefaultReason()
    {
        NodeSays("bad", new AddressValidation { IsValid = false });

        var resolution = await _resolver.ResolveAsync("bad", CancellationToken.None);

        Assert.Equal(AddressResolution.DefaultError, resolution.Error);
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_AddressIsInvalid_THEN_ResultIsNotCached()
    {
        NodeSays("bad", new AddressValidation { IsValid = false });

        await _resolver.ResolveAsync("bad", CancellationToken.None);
        await _resolver.ResolveAsync("bad", CancellationToken.None);

        await _validator.Received(2).ValidateAddressAsync("bad", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_ValidButScriptMissing_THEN_TreatedAsInvalid()
    {
        NodeSays("odd", new AddressValidation { IsValid = true, ScriptPubKey = "" });

        Assert.False((await _resolver.ResolveAsync("odd", CancellationToken.None)).IsValid);
    }
}
