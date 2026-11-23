using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Handlers;

public interface IStratumDispatcher
{
    Task<StratumReply> DispatchAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken);
}
