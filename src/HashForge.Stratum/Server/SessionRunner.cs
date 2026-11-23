using System.Net;
using System.Net.Sockets;
using HashForge.Stratum.Handlers;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.Server;

public sealed partial class SessionRunner(
    IStratumDispatcher dispatcher,
    ISessionRegistry sessions,
    IOptions<StratumOptions> options,
    ILogger<SessionRunner> logger) : ISessionRunner
{
    private readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(options.Value.IdleTimeoutSeconds);

    public async Task RunAsync(StratumSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        sessions.Add(session);
        LogConnected(session.RemoteAddress, session.Pool.Name);
        var reason = "the pool is stopping";
        try
        {
            reason = await ServeAsync(session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown of the pool or of this pool's port.
        }
        catch (OperationCanceledException)
        {
            reason = "idle timeout";
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException or ObjectDisposedException)
        {
            reason = exception.Message;
        }
#pragma warning disable CA1031 // One miner's failure must never take down the server; it is logged and that miner is dropped.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            reason = "internal error";
            LogSessionFailed(exception, session.RemoteAddress);
        }
        finally
        {
            sessions.Remove(session);
            await session.DisposeAsync().ConfigureAwait(false);
            LogDisconnected(session.RemoteAddress, session.Pool.Name, reason);
        }
    }

    /// <summary>Returns the reason the conversation ended.</summary>
    private async Task<string> ServeAsync(StratumSession session, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await ReceiveAsync(session, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return "closed by miner";
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!StratumRequest.TryParse(line, out var request))
            {
                return "malformed request";
            }

            var reply = await dispatcher.DispatchAsync(session, request, cancellationToken).ConfigureAwait(false);
            var message = reply.Error is null
                ? StratumMessages.Response(request.Id, reply.Result)
                : StratumMessages.Error(request.Id, reply.Error);
            await session.Channel.SendAsync(message, cancellationToken).ConfigureAwait(false);

            if (reply.Disconnect)
            {
                return reply.Error?.Message ?? "closed by pool";
            }

            if (reply.FollowUp is not null)
            {
                await reply.FollowUp(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<string?> ReceiveAsync(StratumSession session, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_idleTimeout);
        return await session.Channel.ReceiveAsync(timeout.Token).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Miner {RemoteAddress} connected to pool {Pool}")]
    private partial void LogConnected(IPAddress remoteAddress, string pool);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Miner {RemoteAddress} disconnected from pool {Pool}: {Reason}")]
    private partial void LogDisconnected(IPAddress remoteAddress, string pool, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected failure serving miner {RemoteAddress}")]
    private partial void LogSessionFailed(Exception exception, IPAddress remoteAddress);
}
