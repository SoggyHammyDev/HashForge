using System.Globalization;
using HashForge.Domain;
using HashForge.Mining.Shares;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Logging;

namespace HashForge.Stratum.Handlers;

/// <summary>mining.submit: checks a share and retargets the connection's difficulty when needed.</summary>
public sealed partial class SubmitHandler(
    IShareProcessor shareProcessor,
    IWorkSender workSender,
    TimeProvider timeProvider,
    ILogger<SubmitHandler> logger) : IStratumMethodHandler
{
    private const int UsernameIndex = 0;
    private const int JobIdIndex = 1;
    private const int Extranonce2Index = 2;
    private const int TimeIndex = 3;
    private const int NonceIndex = 4;
    private const int VersionBitsIndex = 5;

    public string Method => "mining.submit";

    public async Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetString(UsernameIndex) is not { } username || session.Workers.Find(username) is not { } worker)
        {
            return StratumReply.Fail(StratumError.Unauthorized);
        }

        var jobId = request.GetString(JobIdIndex) ?? string.Empty;
        var submission = new ShareSubmission(
            session.Pool.Name,
            worker,
            jobId,
            session.Extranonce1,
            request.GetString(Extranonce2Index) ?? string.Empty,
            request.GetString(TimeIndex) ?? string.Empty,
            request.GetString(NonceIndex) ?? string.Empty,
            request.GetString(VersionBitsIndex),
            session.VersionMask,
            session.VarDiff.Difficulty,
            session.VarDiff.PreviousDifficulty)
        {
            JobWasSent = session.Jobs.HasSent(jobId),
        };

        var result = await shareProcessor.ProcessAsync(submission, cancellationToken).ConfigureAwait(false);
        session.Shares.Count(result.IsAccepted);
        if (!result.IsAccepted)
        {
            LogRejection(username, submission, result);
            return StratumReply.Fail(ToError(result.Outcome));
        }

        session.VarDiff.RecordShare();
        return session.VarDiff.TryRetarget(timeProvider.GetUtcNow()) is null
            ? StratumReply.Ok()
            : StratumReply.Ok(followUp: ct => SendNewDifficultyAsync(session, ct));
    }

    /// <summary>A new difficulty only takes effect with the next job, so one is sent straight away.</summary>
    private async Task SendNewDifficultyAsync(StratumSession session, CancellationToken cancellationToken)
    {
        await workSender.SendDifficultyAsync(session, cancellationToken).ConfigureAwait(false);
        await workSender.SendJobAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every rejection is logged with what was needed to accept it, so it can be explained afterwards.</summary>
    private void LogRejection(string username, ShareSubmission submission, ShareResult result)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var worker = LogText.Clean(username);
        var jobId = LogText.Clean(submission.JobId);
        var previous = submission.PreviousDifficulty?.ToString(CultureInfo.InvariantCulture) ?? "none";
        LogShareRejected(
            result.Outcome,
            worker,
            submission.PoolName,
            jobId,
            submission.JobWasSent,
            result.ActualDifficulty,
            submission.Difficulty,
            previous);
    }

    private static StratumError ToError(ShareOutcome outcome) => outcome switch
    {
        ShareOutcome.StaleJob => StratumError.JobNotFound,
        ShareOutcome.Duplicate => StratumError.DuplicateShare,
        ShareOutcome.LowDifficulty => StratumError.LowDifficulty,
        _ => StratumError.Malformed,
    };

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Share rejected as {Outcome} from {Worker} on pool {Pool}: job {JobId} (sent on this connection: {JobWasSent}), "
            + "share difficulty {ShareDifficulty}, required {RequiredDifficulty}, previously {PreviousDifficulty}")]
    private partial void LogShareRejected(
        ShareOutcome outcome,
        string worker,
        string pool,
        string jobId,
        bool jobWasSent,
        double shareDifficulty,
        double requiredDifficulty,
        string previousDifficulty);
}
