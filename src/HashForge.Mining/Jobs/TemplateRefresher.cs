using HashForge.Bitcoin.Rpc;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Jobs;

/// <summary>
/// Fetches block templates from the node and publishes them. Requests can come from several places at once
/// (a pushed block announcement, the poller, a block the pool just found); they are handled one at a time so
/// templates are always published in the order the node produced them.
/// </summary>
public sealed partial class TemplateRefresher(
    IBlockTemplateSource templateSource,
    ITemplateSink sink,
    INodeReadiness readiness,
    TimeProvider timeProvider,
    ILogger<TemplateRefresher> logger) : ITemplateRefresher, IDisposable
{
    /// <summary>Bitcoin Core's RPC_CLIENT_NOT_CONNECTED error code.</summary>
    private const int NodeNotConnected = -9;

    /// <summary>Bitcoin Core's RPC_CLIENT_IN_INITIAL_DOWNLOAD error code.</summary>
    private const int NodeInInitialDownload = -10;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile TemplateStatus _status = new(null, DateTimeOffset.MinValue);

    public TemplateStatus Status => _status;

    public void Dispose() => _gate.Dispose();

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FetchAndPublishAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task FetchAndPublishAsync(CancellationToken cancellationToken)
    {
        BlockTemplate template;
        try
        {
            template = await templateSource.GetBlockTemplateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BitcoinRpcException exception) when (exception.Code is NodeNotConnected or NodeInInitialDownload)
        {
            readiness.ReportNotReady(exception.Message);
            return;
        }

        readiness.ReportReady();
        if (template.PreviousBlockHash != _status.Tip)
        {
            LogNewBlock(template.Height, template.PreviousBlockHash);
        }

        _status = new TemplateStatus(template.PreviousBlockHash, timeProvider.GetUtcNow()) { RewardSatoshis = template.CoinbaseValue };
        sink.Publish(template);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Now mining block {Height} on top of {PreviousBlockHash}")]
    private partial void LogNewBlock(int height, string previousBlockHash);
}