namespace HashForge.Bitcoin.Rpc;

/// <summary>Sends one JSON-RPC call to the node and returns its typed result.</summary>
public interface IBitcoinRpcInvoker
{
    Task<T?> InvokeAsync<T>(string method, IReadOnlyList<object?> parameters, CancellationToken cancellationToken);
}
