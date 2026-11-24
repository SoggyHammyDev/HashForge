using HashForge.Domain;
using HashForge.Mining;
using HashForge.Mining.Payouts;
using HashForge.Mining.Shares;
using HashForge.Stratum.Handlers;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Stratum.UnitTests.Handlers;

public sealed class HandlerTests
{
    private readonly FakeConnection _connection = new();
    private readonly IWorkSender _workSender = Substitute.For<IWorkSender>();
    private readonly IAddressScriptResolver _resolver = Substitute.For<IAddressScriptResolver>();
    private readonly IShareProcessor _processor = Substitute.For<IShareProcessor>();
    private readonly FakeTimeProvider _time = new(StratumTestKit.Now);
    private readonly ILogger<AuthorizeHandler> _authorizeLogger = Substitute.For<ILogger<AuthorizeHandler>>();
    private readonly ILogger<SubmitHandler> _submitLogger = Substitute.For<ILogger<SubmitHandler>>();

    public HandlerTests()
    {
        _authorizeLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _submitLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _resolver.ResolveAsync(StratumTestKit.MinerAddress, Arg.Any<CancellationToken>()).Returns(new AddressResolution(true, [0x51], null));
        _resolver.ResolveAsync("bc1qbadaddress00000", Arg.Any<CancellationToken>()).Returns(new AddressResolution(false, null, "Invalid checksum"));
    }

    private AuthorizeHandler Authorize() => new(_resolver, _workSender, _authorizeLogger);

    private SubmitHandler Submit() => new(_processor, _workSender, _time, _submitLogger);

    private StratumSession SubscribedSession()
    {
        var session = StratumTestKit.Session(_connection);
        session.IsSubscribed = true;
        return session;
    }

    private void ProcessorReturns(ShareOutcome outcome) =>
        _processor.ProcessAsync(Arg.Any<ShareSubmission>(), Arg.Any<CancellationToken>())
            .Returns(call => new ShareResult("solo", call.Arg<ShareSubmission>().Worker, outcome, 1024, 2000, StratumTestKit.Now));

    private static StratumRequest SubmitRequest(string username = "bc1qmineraddress000.rig1", string? versionBits = null) =>
        versionBits is null
            ? StratumTestKit.Request("mining.submit", username, "a1", "0000000000000001", "65000000", "deadbeef")
            : StratumTestKit.Request("mining.submit", username, "a1", "0000000000000001", "65000000", "deadbeef", versionBits);

    [Fact]
    public async Task GIVEN_Subscribe_WHEN_Handled_THEN_SessionIsSubscribedAndExtranonceDetailsReturned()
    {
        var session = StratumTestKit.Session();
        var handler = new SubscribeHandler(Microsoft.Extensions.Options.Options.Create(new MiningOptions { Extranonce2Size = 8 }));

        var reply = await handler.HandleAsync(session, StratumTestKit.Request("mining.subscribe", "cgminer/4.12"), CancellationToken.None);

        Assert.True(session.IsSubscribed);
        Assert.Equal(
            """{"id":1,"result":[[["mining.set_difficulty","01020304"],["mining.notify","01020304"]],"01020304",8],"error":null}""",
            StratumMessages.Response(StratumTestKit.Json("1"), reply.Result));
        Assert.Equal("mining.subscribe", handler.Method);
    }

    [Fact]
    public async Task GIVEN_Configure_WHEN_VersionRollingRequestedWithMask_THEN_MaskIsIntersectedWithPoolMask()
    {
        var session = StratumTestKit.Session();
        var handler = new ConfigureHandler(StratumTestKit.Options());
        var request = StratumTestKit.Request(
            "mining.configure",
            new[] { "version-rolling", "minimum-difficulty" },
            new Dictionary<string, object> { ["version-rolling.mask"] = "ffff0000" });

        var reply = await handler.HandleAsync(session, request, CancellationToken.None);

        Assert.Equal(0x1fff0000u, session.VersionMask);
        Assert.Equal(
            """{"id":1,"result":{"version-rolling":true,"minimum-difficulty":false,"version-rolling.mask":"1fff0000"},"error":null}""",
            StratumMessages.Response(StratumTestKit.Json("1"), reply.Result));
    }

    [Fact]
    public async Task GIVEN_Configure_WHEN_VersionRollingRequestedWithoutMask_THEN_PoolMaskIsUsed()
    {
        var session = StratumTestKit.Session();

        await new ConfigureHandler(StratumTestKit.Options()).HandleAsync(
            session, StratumTestKit.Request("mining.configure", new object[] { new[] { "version-rolling" } }), CancellationToken.None);

        Assert.Equal(0x1fffe000u, session.VersionMask);
    }

    [Fact]
    public async Task GIVEN_Configure_WHEN_NoExtensionsRequested_THEN_ResultIsEmptyAndMaskUnchanged()
    {
        var session = StratumTestKit.Session();

        var reply = await new ConfigureHandler(StratumTestKit.Options()).HandleAsync(
            session, StratumTestKit.Request("mining.configure"), CancellationToken.None);

        Assert.Empty((Dictionary<string, object>)reply.Result!);
        Assert.Equal(0u, session.VersionMask);
    }

    [Fact]
    public async Task GIVEN_ExtranonceSubscribe_WHEN_Handled_THEN_ReturnsTrue()
    {
        var handler = new ExtranonceSubscribeHandler();

        var reply = await handler.HandleAsync(StratumTestKit.Session(), StratumTestKit.Request(handler.Method), CancellationToken.None);

        Assert.Equal(true, reply.Result);
        Assert.Null(reply.Error);
    }

    [Fact]
    public async Task GIVEN_SuggestDifficulty_WHEN_ValueIsValid_THEN_DifficultyIsAppliedAndSentAfterReply()
    {
        var session = StratumTestKit.Session();
        var handler = new SuggestDifficultyHandler(_workSender, _time);

        var reply = await handler.HandleAsync(session, StratumTestKit.Request(handler.Method, 4096), CancellationToken.None);
        await reply.FollowUp!(CancellationToken.None);

        Assert.Equal(4096, session.VarDiff.Difficulty);
        await _workSender.Received(1).SendDifficultyAsync(session, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    [InlineData("text")]
    public async Task GIVEN_SuggestDifficulty_WHEN_ValueIsInvalid_THEN_RepliesMalformedAndKeepsDifficulty(object value)
    {
        var session = StratumTestKit.Session();
        var handler = new SuggestDifficultyHandler(_workSender, _time);

        var reply = await handler.HandleAsync(session, StratumTestKit.Request(handler.Method, value), CancellationToken.None);

        Assert.Equal(StratumError.Malformed, reply.Error);
        Assert.Equal(1024, session.VarDiff.Difficulty);
    }

    [Fact]
    public async Task GIVEN_Authorize_WHEN_NodeAcceptsAddress_THEN_WorkerIsAuthorisedAndWorkFollowsReply()
    {
        var session = SubscribedSession();

        var reply = await Authorize().HandleAsync(
            session, StratumTestKit.Request("mining.authorize", "bc1qmineraddress000.rig1", "x"), CancellationToken.None);

        Assert.Equal(true, reply.Result);
        Assert.False(reply.Disconnect);
        Assert.Equal(new WorkerIdentity(StratumTestKit.MinerAddress, "rig1"), session.Workers.Find("bc1qmineraddress000.rig1"));
        await _workSender.DidNotReceiveWithAnyArgs().SendJobAsync(default!, default);

        await reply.FollowUp!(CancellationToken.None);

        Received.InOrder(() =>
        {
            _workSender.SendDifficultyAsync(session, Arg.Any<CancellationToken>());
            _workSender.SendJobAsync(session, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GIVEN_Authorize_WHEN_NodeRejectsAddress_THEN_ConnectionIsDroppedAndReasonIsLogged()
    {
        var session = SubscribedSession();

        var reply = await Authorize().HandleAsync(
            session, StratumTestKit.Request("mining.authorize", "bc1qbadaddress00000.rig1", "x"), CancellationToken.None);

        Assert.True(reply.Disconnect);
        Assert.Equal(24, reply.Error!.Code);
        Assert.Contains("Invalid checksum", reply.Error.Message);
        Assert.Null(session.Workers.MinerAddress);
        var logged = Assert.Single(_authorizeLogger.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, logged.GetArguments()[0]);
        var message = logged.GetArguments()[2]!.ToString();
        Assert.Contains("192.168.1.50", message);
        Assert.Contains("bc1qbadaddress00000.rig1", message);
        Assert.Contains("the node rejected the address: Invalid checksum", message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".rig1")]
    [InlineData("not an address.rig1")]
    [InlineData("bc1qmineraddress000.rig one")]
    [InlineData("bc1qmineraddress000.<script>")]
    [InlineData("bc1qmineraddress000.a-worker-name-that-is-much-longer-than-allowed")]
    public async Task GIVEN_Authorize_WHEN_UsernameHasNoAddress_THEN_ConnectionIsDroppedWithoutAskingNode(string username)
    {
        var reply = await Authorize().HandleAsync(
            SubscribedSession(), StratumTestKit.Request("mining.authorize", username, "x"), CancellationToken.None);

        Assert.True(reply.Disconnect);
        Assert.Contains("must be a Bitcoin address", reply.Error!.Message);
        await _resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
    }

    [Fact]
    public async Task GIVEN_Authorize_WHEN_NotSubscribed_THEN_RepliesNotSubscribedWithoutDisconnecting()
    {
        var reply = await Authorize().HandleAsync(
            StratumTestKit.Session(), StratumTestKit.Request("mining.authorize", "bc1qmineraddress000", "x"), CancellationToken.None);

        Assert.Equal(StratumError.NotSubscribed, reply.Error);
        Assert.False(reply.Disconnect);
    }

    [Fact]
    public async Task GIVEN_Authorize_WHEN_SecondWorkerUsesAnotherValidAddress_THEN_ConnectionIsDropped()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        _resolver.ResolveAsync("bc1qsecondaddress00", Arg.Any<CancellationToken>()).Returns(new AddressResolution(true, [0x52], null));

        var reply = await Authorize().HandleAsync(
            session, StratumTestKit.Request("mining.authorize", "bc1qsecondaddress00", "x"), CancellationToken.None);

        Assert.True(reply.Disconnect);
        Assert.Contains("different address", reply.Error!.Message);
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_ShareAccepted_THEN_SubmissionCarriesSessionStateAndShareIsCounted()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        session.VersionMask = 0x1fffe000;
        ProcessorReturns(ShareOutcome.Accepted);

        var reply = await Submit().HandleAsync(session, SubmitRequest(versionBits: "00004000"), CancellationToken.None);

        Assert.Equal(true, reply.Result);
        Assert.Null(reply.FollowUp);
        Assert.Equal(1, session.ToInfo().Accepted);
        await _processor.Received(1).ProcessAsync(
            Arg.Is<ShareSubmission>(s =>
                s.PoolName == "solo" && s.Worker.WorkerName == "rig1" && s.JobId == "a1"
                && s.Extranonce1 == session.Extranonce1 && s.Extranonce2Hex == "0000000000000001"
                && s.TimeHex == "65000000" && s.NonceHex == "deadbeef" && s.VersionBitsHex == "00004000"
                && s.VersionMask == 0x1fffe000 && s.Difficulty == 1024 && s.PreviousDifficulty == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_WorkerNotAuthorised_THEN_RepliesUnauthorizedWithoutProcessing()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);

        var reply = await Submit().HandleAsync(session, SubmitRequest("someone-else"), CancellationToken.None);

        Assert.Equal(StratumError.Unauthorized, reply.Error);
        await _processor.DidNotReceiveWithAnyArgs().ProcessAsync(default!, default);
    }

    [Theory]
    [InlineData(ShareOutcome.StaleJob, 21)]
    [InlineData(ShareOutcome.Duplicate, 22)]
    [InlineData(ShareOutcome.LowDifficulty, 23)]
    [InlineData(ShareOutcome.Malformed, 20)]
    public async Task GIVEN_Submit_WHEN_ShareRejected_THEN_RepliesWithMatchingErrorAndCountsRejection(ShareOutcome outcome, int expectedCode)
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        ProcessorReturns(outcome);

        var reply = await Submit().HandleAsync(session, SubmitRequest(), CancellationToken.None);

        Assert.Equal(expectedCode, reply.Error!.Code);
        Assert.Equal((0L, 1L), (session.ToInfo().Accepted, session.ToInfo().Rejected));
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_JobWasSentOnThisConnection_THEN_SubmissionSaysSo()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        await session.Jobs.SendAsync(HandlerTestData.Job(session.Pool), CancellationToken.None);
        ProcessorReturns(ShareOutcome.Accepted);

        await Submit().HandleAsync(session, SubmitRequest(), CancellationToken.None);

        await _processor.Received(1).ProcessAsync(Arg.Is<ShareSubmission>(s => s.JobId == "a1" && s.JobWasSent), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(_submitLogger.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ILogger.Log));
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_JobWasNeverSentOnThisConnection_THEN_SubmissionIsMarkedAndRejectionIsLogged()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        ProcessorReturns(ShareOutcome.StaleJob);

        await Submit().HandleAsync(session, SubmitRequest(), CancellationToken.None);

        await _processor.Received(1).ProcessAsync(Arg.Is<ShareSubmission>(s => s.JobId == "a1" && !s.JobWasSent), Arg.Any<CancellationToken>());
        var logged = Assert.Single(_submitLogger.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Information, logged.GetArguments()[0]);
        Assert.Equal(
            "Share rejected as StaleJob from bc1qmineraddress000.rig1 on pool solo: job a1 (sent on this connection: False), "
            + "share difficulty 2000, required 1024, previously none",
            logged.GetArguments()[2]!.ToString());
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_MissingParameters_THEN_EmptyValuesAreSentForValidation()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        ProcessorReturns(ShareOutcome.Malformed);

        await Submit().HandleAsync(session, StratumTestKit.Request("mining.submit", "bc1qmineraddress000.rig1"), CancellationToken.None);

        await _processor.Received(1).ProcessAsync(
            Arg.Is<ShareSubmission>(s => s.JobId == "" && s.NonceHex == "" && s.VersionBitsHex == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_Submit_WHEN_ShareRateTriggersRetarget_THEN_NewDifficultyAndJobFollowReply()
    {
        var session = StratumTestKit.AuthorizedSession(_connection);
        ProcessorReturns(ShareOutcome.Accepted);
        _time.Advance(TimeSpan.FromSeconds(60));
        var handler = Submit();

        // One share in a minute is far below the target of twelve, so difficulty must fall.
        var reply = await handler.HandleAsync(session, SubmitRequest(), CancellationToken.None);
        await reply.FollowUp!(CancellationToken.None);

        Assert.True(session.VarDiff.Difficulty < 1024);
        Assert.Equal(1024, session.VarDiff.PreviousDifficulty);
        Received.InOrder(() =>
        {
            _workSender.SendDifficultyAsync(session, Arg.Any<CancellationToken>());
            _workSender.SendJobAsync(session, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GIVEN_Dispatch_WHEN_MethodIsRegistered_THEN_ItsHandlerRuns()
    {
        var handler = Substitute.For<IStratumMethodHandler>();
        handler.Method.Returns("mining.test");
        var expected = StratumReply.Ok("done");
        var session = StratumTestKit.Session();
        var request = StratumTestKit.Request("mining.test");
        handler.HandleAsync(session, request, Arg.Any<CancellationToken>()).Returns(expected);

        var reply = await new StratumDispatcher([handler]).DispatchAsync(session, request, CancellationToken.None);

        Assert.Same(expected, reply);
    }

    [Fact]
    public async Task GIVEN_Dispatch_WHEN_MethodIsUnknown_THEN_RepliesUnknownMethod()
    {
        var reply = await new StratumDispatcher([]).DispatchAsync(
            StratumTestKit.Session(), StratumTestKit.Request("mining.nope"), CancellationToken.None);

        Assert.Equal(StratumError.UnknownMethod, reply.Error);
        Assert.False(reply.Disconnect);
    }

    [Fact]
    public void GIVEN_StratumReply_WHEN_BuiltWithHelpers_THEN_DefaultsAreSensible()
    {
        Assert.Equal(true, StratumReply.Ok().Result);
        Assert.Equal("x", StratumReply.Ok("x").Result);
        Assert.True(StratumReply.Fail(StratumError.Malformed, disconnect: true).Disconnect);
        Assert.Null(StratumReply.Fail(StratumError.Malformed).Result);
    }
}
