using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Jobs;
using HashForge.Mining.Shares;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace HashForge.Mining.UnitTests.Shares;

public sealed class ShareProcessorTests
{
    private static readonly ShareValidation Accepted = new(ShareOutcome.Accepted, 64, 100);
    private static readonly BlockCandidate Candidate = new("00ab", "beef");
    private static readonly ShareValidation Solved = new(ShareOutcome.Accepted, 64, 9e13, Candidate);
    private static readonly BlockSubmissionResult NodeAccepted = new(null);

    private readonly IJobSource _jobs = Substitute.For<IJobSource>();
    private readonly IShareValidator _validator = Substitute.For<IShareValidator>();
    private readonly IShareObserver _shareObserver = Substitute.For<IShareObserver>();
    private readonly IBlockCandidateSubmitter _submitterStub = Substitute.For<IBlockCandidateSubmitter>();
    private readonly IFoundBlockRecorder _recorderStub = Substitute.For<IFoundBlockRecorder>();
    private readonly IBlockSubmitter _submitter = Substitute.For<IBlockSubmitter>();
    private readonly IBlockRepository _repository = Substitute.For<IBlockRepository>();
    private readonly IStatisticsReader _statistics = Substitute.For<IStatisticsReader>();
    private readonly IBlockObserver _blockObserver = Substitute.For<IBlockObserver>();
    private readonly ITemplateRefresher _refresher = Substitute.For<ITemplateRefresher>();
    private readonly ILogger<ReliableBlockSubmitter> _handlerLogger = Substitute.For<ILogger<ReliableBlockSubmitter>>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly MiningJob _job = TestData.Job(TestData.Pool("solo", PayoutScheme.Solo));

    public ShareProcessorTests()
    {
        _handlerLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _jobs.Find(_job.Id).Returns(_job);
        _validator.Validate(Arg.Any<ShareSubmission>(), _job).Returns(Accepted);
        _statistics.GetRoundEffortPercent(_job.Pool, TestData.MinerAddress).Returns(87.5);
        _submitterStub.SubmitAsync(_job, Candidate).Returns(NodeAccepted);
        _submitter.SubmitBlockAsync("beef", Arg.Any<CancellationToken>()).Returns(NodeAccepted);
    }

    private ShareProcessor Processor(params IShareObserver[] observers) =>
        new(_jobs, _validator, observers.Length == 0 ? [_shareObserver] : observers, _submitterStub, _recorderStub, _time, NullLogger<ShareProcessor>.Instance);

    private ReliableBlockSubmitter Submitter() => new(_submitter, _time, _handlerLogger);

    private FoundBlockRecorder Recorder(params IBlockObserver[] observers) =>
        new(_repository, _statistics, observers.Length == 0 ? [_blockObserver] : observers, _refresher, NullLogger<FoundBlockRecorder>.Instance);

    /// <summary>Moves the fake clock past each retry delay until the submission finishes.</summary>
    private async Task<BlockSubmissionResult> CompleteWithRetriesAsync(Task<BlockSubmissionResult> submitting)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!submitting.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, deadline.Token);
        }

        return await submitting;
    }

    private static ShareResult Result() => TestData.Share("solo", credited: 64, actual: 9e13);

    private FoundBlock ExpectedBlock(bool accepted = true, string? reason = null) => new(
        "solo", PayoutScheme.Solo, _job.Template.Height, "00ab", TestData.Worker, _job.Template.CoinbaseValue,
        _job.Payouts, 87.5, TestData.Now, accepted, reason) { MinersPaid = _job.MinersPaid };

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_ShareIsValid_THEN_ReturnsAcceptedResultAndNotifiesObservers()
    {
        var result = await Processor().ProcessAsync(TestData.Submission(_job), CancellationToken.None);

        Assert.Equal(new ShareResult("solo", TestData.Worker, ShareOutcome.Accepted, 64, 100, TestData.Now), result);
        _shareObserver.Received(1).OnShare(result);
        await _submitterStub.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default!);
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_JobIsUnknown_THEN_ShareIsStaleAndObserversStillNotified()
    {
        var result = await Processor().ProcessAsync(TestData.Submission(_job) with { JobId = "gone" }, CancellationToken.None);

        Assert.Equal(ShareOutcome.StaleJob, result.Outcome);
        _shareObserver.Received(1).OnShare(result);
        _validator.DidNotReceiveWithAnyArgs().Validate(default!, default!);
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_JobWasNeverSentOnTheConnection_THEN_ShareIsStaleWithoutBeingHashed()
    {
        var result = await Processor().ProcessAsync(TestData.Submission(_job) with { JobWasSent = false }, CancellationToken.None);

        Assert.Equal(ShareOutcome.StaleJob, result.Outcome);
        _validator.DidNotReceiveWithAnyArgs().Validate(default!, default!);
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_JobBelongsToAnotherPool_THEN_ShareIsStale()
    {
        var result = await Processor().ProcessAsync(TestData.Submission(_job, poolName: "pplns"), CancellationToken.None);

        Assert.Equal(ShareOutcome.StaleJob, result.Outcome);
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_ShareIsRejected_THEN_ResultCarriesOutcome()
    {
        _validator.Validate(Arg.Any<ShareSubmission>(), _job).Returns(ShareValidation.Rejected(ShareOutcome.LowDifficulty, 0.5));

        var result = await Processor().ProcessAsync(TestData.Submission(_job), CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(0.5, result.ActualDifficulty);
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_ShareSolvesBlock_THEN_BlockReachesNodeBeforeAnythingElseHappens()
    {
        _validator.Validate(Arg.Any<ShareSubmission>(), _job).Returns(Solved);

        var result = await Processor().ProcessAsync(TestData.Submission(_job), CancellationToken.None);

        Received.InOrder(() =>
        {
            _submitterStub.SubmitAsync(_job, Candidate);
            _shareObserver.OnShare(result);
            _recorderStub.RecordAsync(_job, Candidate, result, NodeAccepted, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GIVEN_ProcessAsync_WHEN_AnObserverThrows_THEN_OtherObserversRunAndBlockIsStillRecorded()
    {
        _validator.Validate(Arg.Any<ShareSubmission>(), _job).Returns(Solved);
        var failing = Substitute.For<IShareObserver>();
        failing.When(o => o.OnShare(Arg.Any<ShareResult>())).Throw(new InvalidOperationException("boom"));

        var result = await Processor(failing, _shareObserver).ProcessAsync(TestData.Submission(_job), CancellationToken.None);

        Assert.True(result.IsAccepted);
        _shareObserver.Received(1).OnShare(result);
        await _recorderStub.Received(1).RecordAsync(_job, Candidate, result, NodeAccepted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_SubmitAsync_WHEN_NodeAnswers_THEN_ReturnsItsVerdictWithoutUsingACancellableToken()
    {
        var verdict = await Submitter().SubmitAsync(_job, Candidate);

        Assert.Same(NodeAccepted, verdict);
        await _submitter.Received(1).SubmitBlockAsync("beef", CancellationToken.None);
    }

    [Fact]
    public async Task GIVEN_SubmitAsync_WHEN_NodeFailsThenRecovers_THEN_SubmissionIsRetried()
    {
        _submitter.SubmitBlockAsync("beef", Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("connection reset"), _ => NodeAccepted);

        var verdict = await CompleteWithRetriesAsync(Submitter().SubmitAsync(_job, Candidate));

        Assert.True(verdict.Accepted);
        await _submitter.Received(2).SubmitBlockAsync("beef", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_SubmitAsync_WHEN_NodeNeverAnswers_THEN_GivesUpAfterAllAttemptsAndLogsTheBlockForManualSubmission()
    {
        _submitter.SubmitBlockAsync("beef", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("node down"));

        var verdict = await CompleteWithRetriesAsync(Submitter().SubmitAsync(_job, Candidate));

        Assert.False(verdict.Accepted);
        Assert.Contains("node down", verdict.RejectReason);
        await _submitter.Received(ReliableBlockSubmitter.SubmitAttempts).SubmitBlockAsync("beef", Arg.Any<CancellationToken>());
        var critical = Assert.Single(
            _handlerLogger.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Critical);
        Assert.Contains("beef", critical.GetArguments()[2]!.ToString());
    }

    [Fact]
    public async Task GIVEN_RecordAsync_WHEN_NodeAcceptedBlock_THEN_BlockIsArchivedAnnouncedAndWorkRefreshed()
    {
        await Recorder().RecordAsync(_job, Candidate, Result(), NodeAccepted, CancellationToken.None);

        var expected = ExpectedBlock();
        await _repository.Received(1).AddAsync(expected, Arg.Any<CancellationToken>());
        await _blockObserver.Received(1).OnBlockFoundAsync(expected, Arg.Any<CancellationToken>());
        await _refresher.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_RecordAsync_WHEN_NodeRejectedBlock_THEN_BlockIsArchivedButNotAnnounced()
    {
        await Recorder().RecordAsync(_job, Candidate, Result(), new BlockSubmissionResult("high-hash"), CancellationToken.None);

        await _repository.Received(1).AddAsync(ExpectedBlock(accepted: false, reason: "high-hash"), Arg.Any<CancellationToken>());
        await _blockObserver.DidNotReceiveWithAnyArgs().OnBlockFoundAsync(default!, default);
        await _refresher.DidNotReceiveWithAnyArgs().RefreshAsync(default);
    }

    [Fact]
    public async Task GIVEN_RecordAsync_WHEN_ArchiveFails_THEN_RoundIsStillClosedAndWorkRefreshed()
    {
        _repository.AddAsync(Arg.Any<FoundBlock>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        await Recorder().RecordAsync(_job, Candidate, Result(), NodeAccepted, CancellationToken.None);

        await _blockObserver.Received(1).OnBlockFoundAsync(ExpectedBlock(), Arg.Any<CancellationToken>());
        await _refresher.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_RecordAsync_WHEN_OneBlockObserverFails_THEN_TheOthersStillRun()
    {
        var failing = Substitute.For<IBlockObserver>();
        failing.OnBlockFoundAsync(Arg.Any<FoundBlock>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await Recorder(failing, _blockObserver).RecordAsync(_job, Candidate, Result(), NodeAccepted, CancellationToken.None);

        await _blockObserver.Received(1).OnBlockFoundAsync(ExpectedBlock(), Arg.Any<CancellationToken>());
        await _refresher.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }
}
