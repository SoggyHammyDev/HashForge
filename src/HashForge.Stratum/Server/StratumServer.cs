using System.Collections.Concurrent;
using System.Net.Sockets;
using HashForge.Domain;
using HashForge.Mining.Pools;
using HashForge.Stratum.Sessions;
using HashForge.Stratum.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.Server;

/// <summary>
/// Listens on one port per pool. Pools added or removed at runtime start and stop
/// their listeners without affecting the others.
/// </summary>
public sealed partial class StratumServer(
    IPoolRegistry registry,
    IConnectionListenerFactory listenerFactory,
    ISessionFactory sessionFactory,
    ISessionRunner sessionRunner,
    ISessionRegistry sessions,
    IOptions<StratumOptions> options,
    ILogger<StratumServer> logger) : IHostedService
{
    private static readonly TimeSpan AcceptRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentDictionary<string, Endpoint> _endpoints = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The names of the pools that are currently accepting connections.</summary>
    public IReadOnlyCollection<string> ListeningPools => [.. _endpoints.Keys];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        registry.PoolAdded += OnPoolAdded;
        registry.PoolRemoved += OnPoolRemoved;
        foreach (var pool in registry.Pools)
        {
            Open(pool);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        registry.PoolAdded -= OnPoolAdded;
        registry.PoolRemoved -= OnPoolRemoved;
        await Task.WhenAll(_endpoints.Keys.Select(CloseAsync)).ConfigureAwait(false);
    }

    private void OnPoolAdded(object? sender, PoolChangedEventArgs args) => Open(args.Pool);

    private void OnPoolRemoved(object? sender, PoolChangedEventArgs args) => _ = CloseAsync(args.Pool.Name);

    private void Open(PoolDefinition pool)
    {
        IConnectionListener listener;
        try
        {
            listener = listenerFactory.Create(pool.Port);
        }
        catch (SocketException exception)
        {
            LogListenFailed(exception, pool.Name, pool.Port);
            return;
        }

        var cancellation = new CancellationTokenSource();
        var endpoint = new Endpoint(listener, cancellation, AcceptLoopAsync(pool, listener, cancellation.Token));
        _endpoints[pool.Name] = endpoint;
        LogListening(pool.Name, pool.Scheme, listener.Port);
    }

    private async Task CloseAsync(string poolName)
    {
        if (!_endpoints.TryRemove(poolName, out var endpoint))
        {
            return;
        }

        await endpoint.Cancellation.CancelAsync().ConfigureAwait(false);
        await endpoint.Listener.DisposeAsync().ConfigureAwait(false);
        await endpoint.AcceptLoop.ConfigureAwait(false);
        endpoint.Cancellation.Dispose();
        LogClosed(poolName);
    }

    private async Task AcceptLoopAsync(PoolDefinition pool, IConnectionListener listener, CancellationToken cancellationToken)
    {
        var running = new ConcurrentDictionary<Task, byte>();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                if (IsOverLimit(connection))
                {
                    LogConnectionRefused(connection.RemoteAddress, pool.Name);
                    await connection.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                var session = sessionRunner.RunAsync(sessionFactory.Create(pool, connection), cancellationToken);
                running[session] = 0;
                _ = session.ContinueWith(t => running.TryRemove(t, out _), TaskScheduler.Default);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
            {
                // The listener was closed, which is how a pool's port is shut down.
                break;
            }
            catch (SocketException exception)
            {
                // One failed accept (a reset connection, a momentary resource shortage) must not close the port.
                LogAcceptFailed(exception, pool.Name);
                await Task.Delay(AcceptRetryDelay, CancellationToken.None).ConfigureAwait(false);
            }
        }

        // Sessions observe the same cancellation token; wait for them so shutdown is orderly.
        await Task.WhenAll(running.Keys).ConfigureAwait(false);
    }

    /// <summary>Applies the connection limits before any per-miner state is created.</summary>
    private bool IsOverLimit(IStratumConnection connection)
    {
        var perAddress = options.Value.MaxConnectionsPerAddress;
        return sessions.Count >= options.Value.MaxConnections
            || (perAddress > 0 && sessions.CountFrom(connection.RemoteAddress) >= perAddress);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Accepting a connection on pool {Pool} failed; still listening")]
    private partial void LogAcceptFailed(Exception exception, string pool);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection from {RemoteAddress} to pool {Pool} refused: connection limit reached")]
    private partial void LogConnectionRefused(System.Net.IPAddress remoteAddress, string pool);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} ({Scheme}) listening for miners on port {Port}")]
    private partial void LogListening(string pool, PayoutScheme scheme, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {Pool} stopped listening")]
    private partial void LogClosed(string pool);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pool {Pool} could not listen on port {Port}")]
    private partial void LogListenFailed(Exception exception, string pool, int port);

    private sealed record Endpoint(IConnectionListener Listener, CancellationTokenSource Cancellation, Task AcceptLoop);
}
