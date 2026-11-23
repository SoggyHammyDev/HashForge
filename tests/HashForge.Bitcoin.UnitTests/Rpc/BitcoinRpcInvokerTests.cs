using System.Net;
using System.Text;
using System.Text.Json;
using HashForge.Bitcoin.Rpc;

namespace HashForge.Bitcoin.UnitTests.Rpc;

public sealed class BitcoinRpcInvokerTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (BitcoinRpcInvoker Invoker, StubHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(BitcoinRpcInvoker.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://node:8332/") });
        return (new BitcoinRpcInvoker(factory), handler);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_NodeReturnsResult_THEN_ResultIsDeserialised()
    {
        var (invoker, _) = Create(HttpStatusCode.OK, """{"result":{"chain":"main","blocks":42},"error":null}""");

        var info = await invoker.InvokeAsync<ChainInfo>("getblockchaininfo", [], CancellationToken.None);

        Assert.Equal("main", info!.Chain);
        Assert.Equal(42, info.Blocks);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_Called_THEN_SendsJsonRpcRequestWithMethodAndParams()
    {
        var (invoker, handler) = Create(HttpStatusCode.OK, """{"result":null,"error":null}""");

        await invoker.InvokeAsync<string>("submitblock", ["abcd"], CancellationToken.None);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("submitblock", request.RootElement.GetProperty("method").GetString());
        Assert.Equal("abcd", request.RootElement.GetProperty("params")[0].GetString());
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_ResultIsNull_THEN_ReturnsNull()
    {
        var (invoker, _) = Create(HttpStatusCode.OK, """{"result":null,"error":null}""");

        Assert.Null(await invoker.InvokeAsync<string>("submitblock", ["abcd"], CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_NodeReturnsRpcErrorWithFailureStatus_THEN_ThrowsBitcoinRpcException()
    {
        var (invoker, _) = Create(
            HttpStatusCode.InternalServerError,
            """{"result":null,"error":{"code":-10,"message":"Bitcoin Core is in initial sync"}}""");

        var exception = await Assert.ThrowsAsync<BitcoinRpcException>(
            () => invoker.InvokeAsync<BlockTemplate>("getblocktemplate", [], CancellationToken.None));

        Assert.Equal(-10, exception.Code);
        Assert.Equal("Bitcoin Core is in initial sync", exception.Message);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_FailureStatusWithoutJsonBody_THEN_ThrowsHttpRequestException()
    {
        var (invoker, _) = Create(HttpStatusCode.Unauthorized, "not json");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => invoker.InvokeAsync<ChainInfo>("getblockchaininfo", [], CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_SuccessStatusWithInvalidJson_THEN_ThrowsJsonException()
    {
        var (invoker, _) = Create(HttpStatusCode.OK, "not json");

        await Assert.ThrowsAsync<JsonException>(
            () => invoker.InvokeAsync<ChainInfo>("getblockchaininfo", [], CancellationToken.None));
    }
}
