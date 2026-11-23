using HashForge.Stratum.Handlers;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Server;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace HashForge.Stratum.UnitTests.Server;

public sealed class SessionRunnerTests
{
    private const string Ping = """{"id":1,"method":"mining.ping","params":[]}""";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeConnection _connection = new();
    private readonly IStratumDispatcher _dispatcher = Substitute.For<IStratumDispatcher>();
    private readonly SessionRegistry _registry = new();
    private readonly StratumSession _session;

    public SessionRunnerTests()
    {
        _session = StratumTestKit.Session(_connection);
        Replies(StratumReply.Ok("pong"));
    }

    private void Replies(StratumReply reply) =>
        _dispatcher.DispatchAsync(_session, Arg.Any<StratumRequest>(), Arg.Any<CancellationToken>()).Returns(reply);

    private Task RunAsync(double idleSeconds = 600, CancellationToken cancellationToken = default) =>
        new SessionRunner(_dispatcher, _registry, StratumTestKit.Options(o => o.IdleTimeoutSeconds = idleSeconds), NullLogger<SessionRunner>.Instance)
            .RunAsync(_session, cancellationToken)
            .WaitAsync(Timeout, CancellationToken.None);

    private void AssertClosedCleanly()
    {
        Assert.True(_connection.Disposed);
        Assert.Empty(_registry.Sessions);
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_MinerSendsRequest_THEN_ReplyIsSentWithRequestId()
    {
        _connection.MinerSends(Ping);
        _connection.MinerDisconnects();

        await RunAsync();

        Assert.Equal("""{"id":1,"result":"pong","error":null}""", Assert.Single(_connection.Sent));
        AssertClosedCleanly();
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_SessionIsActive_THEN_ItIsListedInRegistry()
    {
        var seenDuringDispatch = false;
        _dispatcher.DispatchAsync(_session, Arg.Any<StratumRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                seenDuringDispatch = _registry.Sessions.Contains(_session);
                return StratumReply.Ok();
            });
        _connection.MinerSends(Ping);
        _connection.MinerDisconnects();

        await RunAsync();

        Assert.True(seenDuringDispatch);
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_HandlerReturnsError_THEN_ErrorIsSentAndSessionContinues()
    {
        Replies(StratumReply.Fail(StratumError.LowDifficulty));
        _connection.MinerSends(Ping, Ping);
        _connection.MinerDisconnects();

        await RunAsync();

        Assert.Equal(2, _connection.Sent.Count);
        Assert.All(_connection.Sent, s => Assert.Equal("""{"id":1,"result":null,"error":[23,"Low difficulty share",null]}""", s));
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_ReplyRequestsDisconnect_THEN_ErrorIsSentThenConnectionCloses()
    {
        Replies(StratumReply.Fail(StratumError.Unauthorized, disconnect: true));
        _connection.MinerSends(Ping, Ping);

        await RunAsync();

        Assert.Single(_connection.Sent);
        AssertClosedCleanly();
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_ReplyHasFollowUp_THEN_FollowUpRunsAfterReplyIsSent()
    {
        var sentBeforeFollowUp = -1;
        Replies(StratumReply.Ok(followUp: _ =>
        {
            sentBeforeFollowUp = _connection.Sent.Count;
            return Task.CompletedTask;
        }));
        _connection.MinerSends(Ping);
        _connection.MinerDisconnects();

        await RunAsync();

        Assert.Equal(1, sentBeforeFollowUp);
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_LineIsBlank_THEN_ItIsIgnored()
    {
        _connection.MinerSends("", "   ", Ping);
        _connection.MinerDisconnects();

        await RunAsync();

        Assert.Single(_connection.Sent);
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_LineIsNotJson_THEN_ConnectionIsClosedWithoutDispatching()
    {
        _connection.MinerSends("GET / HTTP/1.1", Ping);

        await RunAsync();

        Assert.Empty(_connection.Sent);
        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default);
        AssertClosedCleanly();
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_MinerIsSilentPastIdleTimeout_THEN_ConnectionIsClosed()
    {
        await RunAsync(idleSeconds: 0.05);

        AssertClosedCleanly();
    }

    [Fact]
    public async Task GIVEN_RunAsync_WHEN_PoolStops_THEN_SessionEndsCleanly()
    {
        using var cancellation = new CancellationTokenSource();
        var running = RunAsync(cancellationToken: cancellation.Token);

        await cancellation.CancelAsync();
        await running;

        AssertClosedCleanly();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task GIVEN_RunAsync_WHEN_HandlerOrConnectionFails_THEN_OnlyThatSessionIsDropped(Type exceptionType)
    {
        _dispatcher.DispatchAsync(_session, Arg.Any<StratumRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync((Exception)Activator.CreateInstance(exceptionType, "failure")!);
        _connection.MinerSends(Ping);

        await RunAsync();

        AssertClosedCleanly();
    }
}
