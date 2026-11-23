using System.Net.Sockets;
using System.Threading.Channels;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Mining.Pools;
using HashForge.Stratum.Server;
using HashForge.Stratum.Sessions;
using HashForge.Stratum.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.UnitTests.Server;

public sealed class StratumServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private sealed class FakeListener(int port) : IConnectionListener
    {
        private readonly Channel<IStratumConnection> _pending = Channel.CreateUnbounded<IStratumConnection>();

        public int Port { get; } = port;

        public bool Disposed { get; private set; }

        public void MinerConnects(IStratumConnection connection) => _pending.Writer.TryWrite(connection);

        public async Task<IStratumConnection> AcceptAsync(CancellationToken cancellationToken) =>
            await _pending.Reader.ReadAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private readonly PoolRegistry _registry = new();
    private readonly Dictionary<int, FakeListener> _listeners = [];
    private readonly IConnectionListenerFactory _listenerFactory = Substitute.For<IConnectionListenerFactory>();
    private readonly ISessionFactory _sessionFactory = Substitute.For<ISessionFactory>();
    private readonly ISessionRunner _runner = Substitute.For<ISessionRunner>();
    private readonly SessionRegistry _sessions = new();
    private readonly StratumOptions _options = new();
    private readonly Channel<StratumSession> _started = Channel.CreateUnbounded<StratumSession>();
    private readonly StratumServer _server;

    public StratumServerTests()
    {
        _listenerFactory.Create(Arg.Any<int>()).Returns(call => _listeners[call.Arg<int>()] = new FakeListener(call.Arg<int>()));
        _sessionFactory.Create(Arg.Any<PoolDefinition>(), Arg.Any<IStratumConnection>())
            .Returns(call => StratumTestKit.Session((FakeConnection)call.Arg<IStratumConnection>(), call.Arg<PoolDefinition>()));
        _runner.RunAsync(Arg.Any<StratumSession>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            _started.Writer.TryWrite(call.Arg<StratumSession>());
            await Task.Delay(System.Threading.Timeout.Infinite, call.Arg<CancellationToken>()).ContinueWith(_ => { }, TaskScheduler.Default);
        });
        _server = new StratumServer(
            _registry, _listenerFactory, _sessionFactory, _runner, _sessions, Options.Create(_options), NullLogger<StratumServer>.Instance);
    }

    private async Task<StratumSession> NextSessionAsync() => await _started.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_PoolsAreRegistered_THEN_EachGetsItsOwnListener()
    {
        _registry.TryAdd(StratumTestKit.Pool("solo", PayoutScheme.Solo, 3333), out _);
        _registry.TryAdd(StratumTestKit.Pool("pplns", PayoutScheme.Pplns, 3334), out _);

        await _server.StartAsync(CancellationToken.None);

        Assert.Equal([3333, 3334], _listeners.Keys.Order());
        Assert.Equal(["pplns", "solo"], _server.ListeningPools.Order());
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_AcceptLoop_WHEN_MinerConnects_THEN_SessionForThatPoolIsRun()
    {
        var pool = StratumTestKit.Pool("pplns", PayoutScheme.Pplns, 3334);
        _registry.TryAdd(pool, out _);
        await _server.StartAsync(CancellationToken.None);

        _listeners[3334].MinerConnects(new FakeConnection());
        var session = await NextSessionAsync();

        Assert.Same(pool, session.Pool);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_PoolAdded_WHEN_ServerIsRunning_THEN_NewPortOpensWithoutRestart()
    {
        await _server.StartAsync(CancellationToken.None);

        _registry.TryAdd(StratumTestKit.Pool("prop", PayoutScheme.Prop, 3335), out _);
        _listeners[3335].MinerConnects(new FakeConnection());

        Assert.Equal("prop", (await NextSessionAsync()).Pool.Name);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_PoolRemoved_WHEN_ServerIsRunning_THEN_ItsListenerClosesAndOthersStay()
    {
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);
        _registry.TryAdd(StratumTestKit.Pool("prop", PayoutScheme.Prop, 3335), out _);
        await _server.StartAsync(CancellationToken.None);

        _registry.Remove("prop");
        await WaitUntilAsync(() => _listeners[3335].Disposed);

        Assert.False(_listeners[3333].Disposed);
        Assert.Equal(["solo"], _server.ListeningPools);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_StopAsync_WHEN_SessionsAreRunning_THEN_TheyAreCancelledAndListenersClosed()
    {
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);
        await _server.StartAsync(CancellationToken.None);
        _listeners[3333].MinerConnects(new FakeConnection());
        await NextSessionAsync();

        await _server.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.True(_listeners[3333].Disposed);
        Assert.Empty(_server.ListeningPools);
    }

    [Fact]
    public async Task GIVEN_StopAsync_WHEN_PoolAddedAfterwards_THEN_NoListenerIsOpened()
    {
        await _server.StartAsync(CancellationToken.None);
        await _server.StopAsync(CancellationToken.None);

        _registry.TryAdd(StratumTestKit.Pool("late", port: 4000), out _);

        Assert.Empty(_listeners);
    }

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_PortIsUnavailable_THEN_OtherPoolsStillStart()
    {
        _listenerFactory.Create(3333).Returns(_ => throw new SocketException((int)SocketError.AddressAlreadyInUse));
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);
        _registry.TryAdd(StratumTestKit.Pool("prop", PayoutScheme.Prop, 3335), out _);

        await _server.StartAsync(CancellationToken.None);

        Assert.Equal(["prop"], _server.ListeningPools);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_AcceptLoop_WHEN_ConnectionLimitIsReached_THEN_NewConnectionIsClosedWithoutASession()
    {
        _options.MaxConnections = 1;
        _sessions.Add(StratumTestKit.Session());
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);
        await _server.StartAsync(CancellationToken.None);
        var refused = new FakeConnection();

        _listeners[3333].MinerConnects(refused);
        await WaitUntilAsync(() => refused.Disposed);

        _sessionFactory.DidNotReceiveWithAnyArgs().Create(default!, default!);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_AcceptLoop_WHEN_PerAddressLimitIsReached_THEN_OnlyThatAddressIsRefused()
    {
        _options.MaxConnectionsPerAddress = 1;
        _sessions.Add(StratumTestKit.Session(new FakeConnection { RemoteAddress = System.Net.IPAddress.Parse("10.0.0.1") }));
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);
        await _server.StartAsync(CancellationToken.None);
        var sameAddress = new FakeConnection { RemoteAddress = System.Net.IPAddress.Parse("10.0.0.1") };

        _listeners[3333].MinerConnects(sameAddress);
        _listeners[3333].MinerConnects(new FakeConnection { RemoteAddress = System.Net.IPAddress.Parse("10.0.0.2") });

        Assert.Equal("10.0.0.2", (await NextSessionAsync()).RemoteAddress.ToString());
        Assert.True(sameAddress.Disposed);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_AcceptLoop_WHEN_AcceptFailsWithSocketError_THEN_PortKeepsListening()
    {
        var failures = 1;
        var listener = Substitute.For<IConnectionListener>();
        var good = new FakeConnection();
        listener.AcceptAsync(Arg.Any<CancellationToken>()).Returns(call =>
            Interlocked.Decrement(ref failures) >= 0
                ? Task.FromException<IStratumConnection>(new SocketException((int)SocketError.ConnectionReset))
                : Volatile.Read(ref failures) == -1
                    ? Task.FromResult<IStratumConnection>(good)
                    : Task.Delay(System.Threading.Timeout.Infinite, call.Arg<CancellationToken>()).ContinueWith<IStratumConnection>(_ => throw new OperationCanceledException(), TaskScheduler.Default));
        _listenerFactory.Create(3333).Returns(listener);
        _registry.TryAdd(StratumTestKit.Pool("solo", port: 3333), out _);

        await _server.StartAsync(CancellationToken.None);

        Assert.Equal("solo", (await NextSessionAsync()).Pool.Name);
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_JobBroadcaster_WHEN_JobsChange_THEN_EverySessionIsSentWorkEvenIfOneFails()
    {
        var jobSource = Substitute.For<IJobSource>();
        var workSender = Substitute.For<IWorkSender>();
        var sessions = new SessionRegistry();
        var failing = StratumTestKit.Session(extranonce1: [0, 0, 0, 1]);
        var healthy = StratumTestKit.Session(extranonce1: [0, 0, 0, 2]);
        sessions.Add(failing);
        sessions.Add(healthy);
        workSender.SendJobAsync(failing, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("gone")));
        using var broadcaster = new JobBroadcaster(jobSource, sessions, workSender, NullLogger<JobBroadcaster>.Instance);
        await broadcaster.StartAsync(CancellationToken.None);

        jobSource.JobsChanged += Raise.EventWith(new JobsChangedEventArgs(isNewBlock: true));
        await broadcaster.StopAsync(CancellationToken.None);

        await workSender.Received(1).SendJobAsync(failing, true, Arg.Any<CancellationToken>());
        await workSender.Received(1).SendJobAsync(healthy, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_JobBroadcaster_WHEN_Stopped_THEN_LaterJobChangesAreIgnored()
    {
        var jobSource = Substitute.For<IJobSource>();
        var workSender = Substitute.For<IWorkSender>();
        var sessions = new SessionRegistry();
        sessions.Add(StratumTestKit.Session());
        using var broadcaster = new JobBroadcaster(jobSource, sessions, workSender, NullLogger<JobBroadcaster>.Instance);
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.StopAsync(CancellationToken.None);

        jobSource.JobsChanged += Raise.EventWith(new JobsChangedEventArgs(isNewBlock: false));

        await workSender.DidNotReceiveWithAnyArgs().SendJobAsync(default!, default, default);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await Task.Delay(10, deadline.Token);
        }
    }
}
