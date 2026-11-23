using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Handlers;

/// <summary>mining.suggest_difficulty: lets a miner ask for a starting difficulty.</summary>
public sealed class SuggestDifficultyHandler(IWorkSender workSender, TimeProvider timeProvider) : IStratumMethodHandler
{
    public string Method => "mining.suggest_difficulty";

    public Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetNumber(0) is not { } requested || requested <= 0 || !double.IsFinite(requested))
        {
            return Task.FromResult(StratumReply.Fail(StratumError.Malformed));
        }

        session.VarDiff.SetDifficulty(requested, timeProvider.GetUtcNow());
        return Task.FromResult(StratumReply.Ok(followUp: ct => workSender.SendDifficultyAsync(session, ct)));
    }
}
