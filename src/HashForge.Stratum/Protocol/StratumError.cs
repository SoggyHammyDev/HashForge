namespace HashForge.Stratum.Protocol;

/// <summary>A Stratum error, sent to miners as <c>[code, message, null]</c>.</summary>
public sealed record StratumError(int Code, string Message)
{
    public static readonly StratumError UnknownMethod = new(20, "Unknown method");
    public static readonly StratumError Malformed = new(20, "Malformed request");
    public static readonly StratumError InternalError = new(20, "Internal error");
    public static readonly StratumError JobNotFound = new(21, "Job not found");
    public static readonly StratumError DuplicateShare = new(22, "Duplicate share");
    public static readonly StratumError LowDifficulty = new(23, "Low difficulty share");
    public static readonly StratumError Unauthorized = new(24, "Unauthorized worker");
    public static readonly StratumError NotSubscribed = new(25, "Not subscribed");

    public object?[] ToWire() => [Code, Message, null];
}
