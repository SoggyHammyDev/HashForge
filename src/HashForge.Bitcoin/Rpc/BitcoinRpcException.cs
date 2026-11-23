namespace HashForge.Bitcoin.Rpc;

public sealed class BitcoinRpcException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
