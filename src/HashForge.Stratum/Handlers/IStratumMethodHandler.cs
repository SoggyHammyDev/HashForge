using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Handlers;

/// <summary>Handles one Stratum method. Add a new method by registering another implementation.</summary>
public interface IStratumMethodHandler
{
    string Method { get; }

    Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken);
}
