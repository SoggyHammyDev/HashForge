namespace HashForge.Bitcoin.Rpc;

/// <summary>The Bitcoin Core RPC calls the pool relies on.</summary>
public sealed class BitcoinNodeClient(IBitcoinRpcInvoker invoker)
    : IBlockTemplateSource, IBlockSubmitter, IChainInfoSource, IChainTipSource, IAddressValidator, IBlockHeaderSource
{
    /// <summary>Bitcoin Core's error code for a block or address it does not know.</summary>
    public const int NotFoundErrorCode = -5;

    private static readonly object TemplateRequest = new { rules = new[] { "segwit" } };

    public Task<BlockTemplate> GetBlockTemplateAsync(CancellationToken cancellationToken) =>
        InvokeRequiredAsync<BlockTemplate>("getblocktemplate", [TemplateRequest], cancellationToken);

    public async Task<BlockSubmissionResult> SubmitBlockAsync(string blockHex, CancellationToken cancellationToken)
    {
        var rejectReason = await invoker.InvokeAsync<string>("submitblock", [blockHex], cancellationToken).ConfigureAwait(false);
        return new BlockSubmissionResult(rejectReason);
    }

    public Task<ChainInfo> GetChainInfoAsync(CancellationToken cancellationToken) =>
        InvokeRequiredAsync<ChainInfo>("getblockchaininfo", [], cancellationToken);

    public Task<double> GetNetworkHashrateAsync(CancellationToken cancellationToken) =>
        invoker.InvokeAsync<double>("getnetworkhashps", [], cancellationToken);

    public Task<string> GetBestBlockHashAsync(CancellationToken cancellationToken) =>
        InvokeRequiredAsync<string>("getbestblockhash", [], cancellationToken);

    public Task<AddressValidation> ValidateAddressAsync(string address, CancellationToken cancellationToken) =>
        InvokeRequiredAsync<AddressValidation>("validateaddress", [address], cancellationToken);

    public async Task<BlockHeaderInfo?> FindBlockHeaderAsync(string hash, CancellationToken cancellationToken)
    {
        try
        {
            return await invoker.InvokeAsync<BlockHeaderInfo>("getblockheader", [hash], cancellationToken).ConfigureAwait(false);
        }
        catch (BitcoinRpcException exception) when (exception.Code == NotFoundErrorCode)
        {
            return null;
        }
    }

    private async Task<T> InvokeRequiredAsync<T>(string method, IReadOnlyList<object?> parameters, CancellationToken cancellationToken)
        where T : class
    {
        var result = await invoker.InvokeAsync<T>(method, parameters, cancellationToken).ConfigureAwait(false);
        return result ?? throw new BitcoinRpcException(0, $"The node returned no result for '{method}'.");
    }
}

