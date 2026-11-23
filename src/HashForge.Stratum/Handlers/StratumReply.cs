using HashForge.Stratum.Protocol;

namespace HashForge.Stratum.Handlers;

/// <summary>What a handler wants sent back for a request.</summary>
/// <param name="FollowUp">Work to do after the reply has been sent, such as pushing the first job.</param>
public sealed record StratumReply(
    object? Result,
    StratumError? Error = null,
    bool Disconnect = false,
    Func<CancellationToken, Task>? FollowUp = null)
{
    public static StratumReply Ok(object? result = null, Func<CancellationToken, Task>? followUp = null) =>
        new(result ?? true, FollowUp: followUp);

    public static StratumReply Fail(StratumError error, bool disconnect = false) => new(null, error, disconnect);
}
