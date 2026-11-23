using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Handlers;

/// <summary>Routes each request to the handler registered for its method.</summary>
public sealed class StratumDispatcher(IEnumerable<IStratumMethodHandler> handlers) : IStratumDispatcher
{
    private readonly Dictionary<string, IStratumMethodHandler> _handlers =
        handlers.ToDictionary(h => h.Method, StringComparer.Ordinal);

    public Task<StratumReply> DispatchAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _handlers.TryGetValue(request.Method, out var handler)
            ? handler.HandleAsync(session, request, cancellationToken)
            : Task.FromResult(StratumReply.Fail(StratumError.UnknownMethod));
    }
}
