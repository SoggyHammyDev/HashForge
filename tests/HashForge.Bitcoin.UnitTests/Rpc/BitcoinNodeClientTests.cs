using System.Text.Json;
using HashForge.Bitcoin.Rpc;

namespace HashForge.Bitcoin.UnitTests.Rpc;

public sealed class BitcoinNodeClientTests
{
    private readonly IBitcoinRpcInvoker _invoker = Substitute.For<IBitcoinRpcInvoker>();
    private readonly BitcoinNodeClient _client;

    public BitcoinNodeClientTests() => _client = new BitcoinNodeClient(_invoker);

    [Fact]
    public async Task GIVEN_GetBlockTemplateAsync_WHEN_Called_THEN_RequestsTemplateWithSegwitRule()
    {
        var template = new BlockTemplate
        {
            Version = 1, PreviousBlockHash = "00", CoinbaseValue = 1, Bits = "1d00ffff", CurrentTime = 1, Height = 1,
        };
        IReadOnlyList<object?>? sentParameters = null;
        _invoker.InvokeAsync<BlockTemplate>("getblocktemplate", Arg.Do<IReadOnlyList<object?>>(p => sentParameters = p), Arg.Any<CancellationToken>())
            .Returns(template);

        var result = await _client.GetBlockTemplateAsync(CancellationToken.None);

        Assert.Same(template, result);
        Assert.Equal("""[{"rules":["segwit"]}]""", JsonSerializer.Serialize(sentParameters));
    }

    [Fact]
    public async Task GIVEN_GetBlockTemplateAsync_WHEN_NodeReturnsNoResult_THEN_ThrowsBitcoinRpcException()
    {
        _invoker.InvokeAsync<BlockTemplate>(default!, default!, default).ReturnsForAnyArgs((BlockTemplate?)null);

        await Assert.ThrowsAsync<BitcoinRpcException>(() => _client.GetBlockTemplateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_SubmitBlockAsync_WHEN_NodeReturnsNull_THEN_BlockIsAccepted()
    {
        _invoker.InvokeAsync<string>("submitblock", Arg.Is<IReadOnlyList<object?>>(p => (string)p[0]! == "beef"), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _client.SubmitBlockAsync("beef", CancellationToken.None);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task GIVEN_SubmitBlockAsync_WHEN_NodeReturnsReason_THEN_BlockIsRejectedWithReason()
    {
        _invoker.InvokeAsync<string>(default!, default!, default).ReturnsForAnyArgs("high-hash");

        var result = await _client.SubmitBlockAsync("beef", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("high-hash", result.RejectReason);
    }

    [Fact]
    public async Task GIVEN_GetChainInfoAsync_WHEN_Called_THEN_ReturnsNodeChainInfo()
    {
        var info = new ChainInfo { Chain = "regtest", Blocks = 7 };
        _invoker.InvokeAsync<ChainInfo>("getblockchaininfo", Arg.Any<IReadOnlyList<object?>>(), Arg.Any<CancellationToken>()).Returns(info);

        Assert.Same(info, await _client.GetChainInfoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_GetNetworkHashrateAsync_WHEN_Called_THEN_ReturnsNodeValue()
    {
        _invoker.InvokeAsync<double>("getnetworkhashps", Arg.Any<IReadOnlyList<object?>>(), Arg.Any<CancellationToken>()).Returns(123.5);

        Assert.Equal(123.5, await _client.GetNetworkHashrateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_GetBestBlockHashAsync_WHEN_Called_THEN_ReturnsNodeTip()
    {
        _invoker.InvokeAsync<string>("getbestblockhash", Arg.Any<IReadOnlyList<object?>>(), Arg.Any<CancellationToken>()).Returns("00ab");

        Assert.Equal("00ab", await _client.GetBestBlockHashAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ValidateAddressAsync_WHEN_Called_THEN_PassesAddressToNode()
    {
        var validation = new AddressValidation { IsValid = true, ScriptPubKey = "0014ab" };
        _invoker.InvokeAsync<AddressValidation>("validateaddress", Arg.Is<IReadOnlyList<object?>>(p => (string)p[0]! == "bc1q"), Arg.Any<CancellationToken>())
            .Returns(validation);

        Assert.Same(validation, await _client.ValidateAddressAsync("bc1q", CancellationToken.None));
    }
}

