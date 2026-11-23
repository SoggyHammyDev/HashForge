using Microsoft.Extensions.Logging;

namespace HashForge.Mining.State;

/// <summary>Copies the state of every <see cref="IStatePersistable"/> to and from the store.</summary>
public sealed partial class HotStateCoordinator(
    IEnumerable<IStatePersistable> participants,
    IHotStateStore store,
    ILogger<HotStateCoordinator> logger) : IHotStateCoordinator
{
    public async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        foreach (var participant in participants)
        {
            await store.SaveAsync(participant.StateKey, participant.ExportState(), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RestoreAllAsync(CancellationToken cancellationToken)
    {
        foreach (var participant in participants)
        {
            var state = await store.LoadAsync(participant.StateKey, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                continue;
            }

            participant.ImportState(state);
            LogRestored(participant.StateKey);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored {StateKey} state from the previous run")]
    private partial void LogRestored(string stateKey);
}
