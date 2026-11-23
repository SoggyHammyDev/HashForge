using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

/// <summary>Talks JSON-RPC to Bitcoin Core over HTTP.</summary>
public sealed class BitcoinRpcInvoker(IHttpClientFactory httpClientFactory) : IBitcoinRpcInvoker
{
    public const string HttpClientName = "BitcoinNode";

    private const string JsonRpcVersion = "1.0";
    private const string RequestId = "hashforge";

    public async Task<T?> InvokeAsync<T>(string method, IReadOnlyList<object?> parameters, CancellationToken cancellationToken)
    {
        var request = new RpcRequest(JsonRpcVersion, RequestId, method, parameters);
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsJsonAsync(string.Empty, request, cancellationToken).ConfigureAwait(false);

        // Bitcoin Core reports RPC errors with a non-success status and a JSON body, so read the body first.
        var envelope = await ReadEnvelopeAsync<T>(response, cancellationToken).ConfigureAwait(false);
        if (envelope?.Error is { } error)
        {
            throw new BitcoinRpcException(error.Code, error.Message);
        }

        response.EnsureSuccessStatusCode();
        return envelope is null ? default : envelope.Result;
    }

    private static async Task<RpcEnvelope<T>?> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<RpcEnvelope<T>>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            response.EnsureSuccessStatusCode();
            throw;
        }
    }

    private sealed record RpcRequest(
        [property: JsonPropertyName("jsonrpc")] string JsonRpc,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] IReadOnlyList<object?> Params);

    private sealed record RpcError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string Message);

    private sealed record RpcEnvelope<T>(
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("error")] RpcError? Error);
}

