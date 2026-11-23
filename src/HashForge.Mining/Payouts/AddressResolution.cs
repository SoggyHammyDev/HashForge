namespace HashForge.Mining.Payouts;

/// <summary>The node's verdict on a payout address, with its output script when valid.</summary>
public sealed record AddressResolution(bool IsValid, byte[]? ScriptPubKey, string? Error)
{
    public const string DefaultError = "the node does not recognise this as a Bitcoin address for its network";
}
