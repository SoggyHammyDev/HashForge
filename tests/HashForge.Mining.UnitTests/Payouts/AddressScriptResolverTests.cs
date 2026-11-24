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

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_ManyAskAboutTheSameAddressAtOnce_THEN_NodeIsAskedOnce()
    {
        var node = new TaskCompletionSource<AddressValidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        _validator.ValidateAddressAsync("busy", Arg.Any<CancellationToken>()).Returns(node.Task);

        var waiting = Enumerable.Range(0, 50).Select(_ => _resolver.ResolveAsync("busy", CancellationToken.None)).ToList();
        node.SetResult(new AddressValidation { IsValid = true, ScriptPubKey = "51" });
        var resolutions = await Task.WhenAll(waiting);

        Assert.All(resolutions, resolution => Assert.True(resolution.IsValid));
        await _validator.Received(1).ValidateAddressAsync("busy", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_ManyDifferentAddressesArriveAtOnce_THEN_OnlyAFewAreAskedTogether()
    {
        var gate = new TaskCompletionSource<AddressValidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        _validator.ValidateAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref asked);
            return gate.Task;
        });

        var waiting = Enumerable.Range(0, 40).Select(i => _resolver.ResolveAsync($"address{i}", CancellationToken.None)).ToList();
        var askedWhileNodeIsBusy = Volatile.Read(ref asked);
        gate.SetResult(new AddressValidation { IsValid = true, ScriptPubKey = "51" });
        await Task.WhenAll(waiting);

        Assert.Equal(AddressScriptResolver.MaxConcurrentLookups, askedWhileNodeIsBusy);
        Assert.Equal(40, asked);
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_OneCallerGivesUp_THEN_OthersStillGetTheAnswer()
    {
        var node = new TaskCompletionSource<AddressValidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        _validator.ValidateAddressAsync("slow", Arg.Any<CancellationToken>()).Returns(node.Task);
        using var impatient = new CancellationTokenSource();

        var abandoned = _resolver.ResolveAsync("slow", impatient.Token);
        var patient = _resolver.ResolveAsync("slow", CancellationToken.None);
        await impatient.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        node.SetResult(new AddressValidation { IsValid = true, ScriptPubKey = "51" });

        Assert.True((await patient).IsValid);
    }

    [Fact]
    public async Task GIVEN_ResolveAsync_WHEN_NodeFailsOnce_THEN_NextCallAsksAgain()
    {
        _validator.ValidateAddressAsync("flaky", Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<AddressValidation>(new HttpRequestException("node unavailable")),
                _ => Task.FromResult(new AddressValidation { IsValid = true, ScriptPubKey = "51" }));

        await Assert.ThrowsAsync<HttpRequestException>(() => _resolver.ResolveAsync("flaky", CancellationToken.None));
        var second = await _resolver.ResolveAsync("flaky", CancellationToken.None);

        Assert.True(second.IsValid);
    }
}
