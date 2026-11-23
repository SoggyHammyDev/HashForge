using System.Text.Json;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Stratum.UnitTests.Sessions;

public sealed class SessionTests
{
    private readonly FakeConnection _connection = new();
    private readonly IJobSource _jobSource = Substitute.For<IJobSource>();
    private readonly FakeTimeProvider _time = new(StratumTestKit.Now);

    [Fact]
    public void GIVEN_Session_WHEN_Created_THEN_IdIsExtranonceHexAndNothingIsAuthorised()
    {
        var session = StratumTestKit.Session(_connection);

        Assert.Equal("01020304", session.Id);
        Assert.Null(session.Workers.MinerAddress);
        Assert.False(session.IsSubscribed);
        Assert.Equal(0u, session.VersionMask);
        Assert.Equal(_connection.RemoteAddress, session.RemoteAddress);
    }

    [Fact]
    public void GIVEN_TryAuthorize_WHEN_WorkersShareOneAddress_THEN_AllAreAccepted()
    {
        var session = StratumTestKit.Session();

        Assert.Equal(AuthorizeOutcome.Authorized, session.Workers.Authorize("addr.rig1", new WorkerIdentity("addr", "rig1")));
        Assert.Equal(AuthorizeOutcome.Authorized, session.Workers.Authorize("addr.rig2", new WorkerIdentity("addr", "rig2")));

        Assert.Equal("addr", session.Workers.MinerAddress);
        Assert.Equal("rig2", session.Workers.Find("addr.rig2")!.WorkerName);
        Assert.Null(session.Workers.Find("unknown"));
    }

    [Fact]
    public void GIVEN_TryAuthorize_WHEN_SecondWorkerUsesDifferentAddress_THEN_ItIsRefused()
    {
        var session = StratumTestKit.Session();
        session.Workers.Authorize("addr.rig1", new WorkerIdentity("addr", "rig1"));

        Assert.Equal(AuthorizeOutcome.DifferentAddress, session.Workers.Authorize("other.rig1", new WorkerIdentity("other", "rig1")));
        Assert.Null(session.Workers.Find("other.rig1"));
    }

    [Fact]
    public void GIVEN_ToInfo_WHEN_SharesCounted_THEN_ReflectsSessionState()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        session.Workers.Authorize("bc1qmineraddress000.rig0", new WorkerIdentity(StratumTestKit.MinerAddress, "rig0"));
        session.Shares.Count(accepted: true);
        session.Shares.Count(accepted: true);
        session.Shares.Count(accepted: false);

        var info = session.ToInfo();

        Assert.Equal(
            ("01020304", "solo", _connection.RemoteAddress, StratumTestKit.MinerAddress, 1024d, 2L, 1L, StratumTestKit.Now),
            (info.Id, info.PoolName, info.RemoteAddress, info.MinerAddress, info.Difficulty, info.Accepted, info.Rejected, info.ConnectedAt));
        Assert.Equal(["rig0", "rig1"], info.Workers);
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_CalledConcurrently_THEN_EveryMessageIsDelivered()
    {
        var session = StratumTestKit.Session(_connection);

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => session.Channel.SendAsync($"m{i}", CancellationToken.None)));

        Assert.Equal(50, _connection.Sent.Distinct().Count());
    }

    [Fact]
    public async Task GIVEN_ReceiveAsync_WHEN_MinerSendsLine_THEN_LineIsReturned()
    {
        var session = StratumTestKit.Session(_connection);
        _connection.MinerSends("hello");

        Assert.Equal("hello", await session.Channel.ReceiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_DisposeAsync_WHEN_Called_THEN_ConnectionIsClosed()
    {
        await StratumTestKit.Session(_connection).DisposeAsync();

        Assert.True(_connection.Disposed);
    }

    [Fact]
    public void GIVEN_SessionRegistry_WHEN_SessionsAddedAndRemoved_THEN_ListTracksThem()
    {
        var registry = new SessionRegistry();
        var first = StratumTestKit.Session(extranonce1: [0, 0, 0, 1]);
        var second = StratumTestKit.Session(extranonce1: [0, 0, 0, 2]);

        registry.Add(first);
        registry.Add(second);
        registry.Remove(first);
        registry.Remove(first);

        Assert.Same(second, Assert.Single(registry.Sessions));
    }

    [Fact]
    public void GIVEN_SessionFactoryCreate_WHEN_CalledRepeatedly_THEN_EachSessionGetsUniqueExtranonceAndInitialDifficulty()
    {
        var factory = new SessionFactory(StratumTestKit.Options(o => o.InitialDifficulty = 2048), new FakeTimeProvider(StratumTestKit.Now));
        var pool = StratumTestKit.Pool();

        var sessions = Enumerable.Range(0, 100).Select(_ => factory.Create(pool, new FakeConnection())).ToList();

        Assert.Equal(100, sessions.Select(s => s.Id).Distinct().Count());
        Assert.All(sessions, s => Assert.Equal(JobFactory.Extranonce1Size, s.Extranonce1.Length));
        Assert.Equal(2048, sessions[0].VarDiff.Difficulty);
        Assert.Equal(StratumTestKit.Now, sessions[0].ConnectedAt);
        Assert.Same(pool, sessions[0].Pool);
    }

    [Fact]
    public async Task GIVEN_SendDifficultyAsync_WHEN_Called_THEN_SendsCurrentDifficulty()
    {
        var session = StratumTestKit.Session(_connection);

        await new WorkSender(_jobSource, _time).SendDifficultyAsync(session, CancellationToken.None);

        Assert.Equal("""{"id":null,"method":"mining.set_difficulty","params":[1024]}""", Assert.Single(_connection.Sent));
    }

    [Fact]
    public async Task GIVEN_SendJobAsync_WHEN_SessionIsAuthorised_THEN_SendsNotifyForMinersJob()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        var job = HandlerTestData.Job(session.Pool);
        _jobSource.GetCurrentJobAsync(session.Pool, StratumTestKit.MinerAddress, Arg.Any<CancellationToken>()).Returns(job);

        await new WorkSender(_jobSource, _time).SendJobAsync(session, CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.Single(_connection.Sent));
        Assert.Equal("mining.notify", document.RootElement.GetProperty("method").GetString());
        Assert.Equal(job.Id, document.RootElement.GetProperty("params")[0].GetString());
        Assert.True(document.RootElement.GetProperty("params")[8].GetBoolean());
    }

    [Fact]
    public async Task GIVEN_SendJobAsync_WHEN_SessionNotAuthorisedOrNoJobYet_THEN_NothingIsSent()
    {
        var sender = new WorkSender(_jobSource, _time);
        _jobSource.GetCurrentJobAsync(default!, default!, default).ReturnsForAnyArgs((MiningJob?)null);

        await sender.SendJobAsync(StratumTestKit.Session(_connection), CancellationToken.None);
        await sender.SendJobAsync(StratumTestKit.AuthorizedSession(_connection), CancellationToken.None);

        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public void GIVEN_Authorize_WHEN_ConnectionAlreadyHasMaximumWorkers_THEN_NewWorkerIsRefusedButKnownOneStillWorks()
    {
        var session = StratumTestKit.Session();
        for (var i = 0; i < WorkerRoster.MaxWorkers; i++)
        {
            session.Workers.Authorize($"addr.rig{i}", new WorkerIdentity("addr", $"rig{i}"));
        }

        Assert.Equal(AuthorizeOutcome.TooManyWorkers, session.Workers.Authorize("addr.extra", new WorkerIdentity("addr", "extra")));
        Assert.Equal(AuthorizeOutcome.Authorized, session.Workers.Authorize("addr.rig0", new WorkerIdentity("addr", "rig0")));
        Assert.Equal(WorkerRoster.MaxWorkers, session.ToInfo().Workers.Count);
    }

    [Fact]
    public async Task GIVEN_SendAsync_WHEN_MinerStopsReading_THEN_SendIsAbandonedAfterTimeout()
    {
        var stalled = Substitute.For<HashForge.Stratum.Transport.IStratumConnection>();
        stalled.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));
        var channel = new MinerChannel(stalled, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => channel.SendAsync("job", CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_SendJobAsync_WHEN_MinerHasSubmittedNothingForAWholeWindow_THEN_DifficultyIsLoweredBeforeTheJob()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        _jobSource.GetCurrentJobAsync(session.Pool, StratumTestKit.MinerAddress, Arg.Any<CancellationToken>())
            .Returns(HandlerTestData.Job(session.Pool));
        _time.Advance(TimeSpan.FromSeconds(60));

        await new WorkSender(_jobSource, _time).SendJobAsync(session, CancellationToken.None);

        Assert.True(session.VarDiff.Difficulty < 1024);
        Assert.Equal(2, _connection.Sent.Count);
        Assert.Contains("mining.set_difficulty", _connection.Sent[0]);
        Assert.Contains("mining.notify", _connection.Sent[1]);
    }
}