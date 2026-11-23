using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.Handlers;

/// <summary>mining.subscribe: tells the miner its extranonce1 and how many extranonce2 bytes it may vary.</summary>
public sealed class SubscribeHandler(IOptions<Mining.MiningOptions> miningOptions) : IStratumMethodHandler
{
    public string Method => "mining.subscribe";

    public Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.IsSubscribed = true;

        object[][] subscriptions =
        [
            [JobNotification.SetDifficultyMethod, session.Id],
            [JobNotification.NotifyMethod, session.Id],
        ];
        object[] result = [subscriptions, session.Id, miningOptions.Value.Extranonce2Size];
        return Task.FromResult(StratumReply.Ok(result));
    }
}
