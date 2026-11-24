using HashForge.Domain.Formatting;
using HashForge.Mining.Shares;
using HashForge.Mining.Statistics;

namespace HashForge.Web.Api;

/// <summary>
/// Shortens payout addresses in everything the public API lists, so the API cannot be used to harvest the
/// addresses of a pool's miners. A miner who already knows their address can still ask for their own figures.
/// </summary>
public static class AddressMasking
{
    private const char WorkerSeparator = '.';

    /// <summary>The block with the finder's address and every payout address shortened.</summary>
    public static FoundBlock Masked(this FoundBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block with
        {
            Worker = block.Worker with { Address = Display.Shorten(block.Worker.Address) },
            Payouts = [.. block.Payouts.Select(payout => payout with { Address = Display.Shorten(payout.Address) })],
        };
    }

    /// <summary>The pool with the address of its best share's worker shortened.</summary>
    public static PoolSnapshot Masked(this PoolSnapshot pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return pool.Stats.BestShare is { } best
            ? pool with { Stats = pool.Stats with { BestShare = best with { Worker = MaskWorker(best.Worker) } } }
            : pool;
    }

    /// <summary>Shortens the address in <c>address.worker</c> and leaves the worker name as it is.</summary>
    public static string MaskWorker(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        var separator = fullName.IndexOf(WorkerSeparator, StringComparison.Ordinal);
        return separator < 0
            ? Display.Shorten(fullName)
            : string.Concat(Display.Shorten(fullName[..separator]), fullName.AsSpan(separator));
    }
}
