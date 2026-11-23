using System.Net;
using HashForge.Domain;
using HashForge.Mining.Payouts;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Logging;

namespace HashForge.Stratum.Handlers;

/// <summary>
/// mining.authorize: the username must be a Bitcoin address (optionally followed by ".worker").
/// The address is verified by the node; anything the node does not accept gets the connection dropped.
/// </summary>
public sealed partial class AuthorizeHandler(
    IAddressScriptResolver addressResolver,
    IWorkSender workSender,
    ILogger<AuthorizeHandler> logger) : IStratumMethodHandler
{
    private const string MissingAddressReason =
        "the username must be a Bitcoin address, optionally followed by a dot and a worker name of letters, digits, '.', '_' or '-'";
    private const string DifferentAddressReason = "this connection is already mining for a different address";
    private const string TooManyWorkersReason = "this connection has authorised too many workers";

    public string Method => "mining.authorize";

    public async Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (!session.IsSubscribed)
        {
            return StratumReply.Fail(StratumError.NotSubscribed);
        }

        var username = request.GetString(0);
        if (!WorkerIdentity.TryParse(username, out var worker))
        {
            return Reject(session, username, MissingAddressReason);
        }

        var address = await addressResolver.ResolveAsync(worker.Address, cancellationToken).ConfigureAwait(false);
        if (!address.IsValid)
        {
            return Reject(session, username, $"the node rejected the address: {address.Error}");
        }

        var outcome = session.Authorize(username!, worker);
        if (outcome != AuthorizeOutcome.Authorized)
        {
            return Reject(session, username, outcome == AuthorizeOutcome.TooManyWorkers ? TooManyWorkersReason : DifferentAddressReason);
        }

        LogAuthorized(session.RemoteAddress, worker.FullName, session.Pool.Name);
        return StratumReply.Ok(followUp: async ct =>
        {
            await workSender.SendDifficultyAsync(session, ct).ConfigureAwait(false);
            await workSender.SendJobAsync(session, cleanJobs: true, ct).ConfigureAwait(false);
        });
    }

    private StratumReply Reject(StratumSession session, string? username, string reason)
    {
        LogRejected(session.RemoteAddress, session.Pool.Name, LogText.Clean(username), reason);
        return StratumReply.Fail(new StratumError(StratumError.Unauthorized.Code, $"Connection refused: {reason}"), disconnect: true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Miner {RemoteAddress} authorised as {Worker} on pool {Pool}")]
    private partial void LogAuthorized(IPAddress remoteAddress, string worker, string pool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection from {RemoteAddress} to pool {Pool} rejected for username '{Username}': {Reason}")]
    private partial void LogRejected(IPAddress remoteAddress, string pool, string username, string reason);
}
