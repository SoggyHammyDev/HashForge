using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Handlers;

/// <summary>mining.extranonce.subscribe: accepted for compatibility; the extranonce never changes mid-session.</summary>
public sealed class ExtranonceSubscribeHandler : IStratumMethodHandler
{
    public string Method => "mining.extranonce.subscribe";

    public Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(StratumReply.Ok());
}
