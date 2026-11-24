using HashForge.Mining.Jobs;

namespace HashForge.Mining.Network;

/// <summary>
/// The difficulty of the block being mined, taken from the block template rather than from the chain tip.
/// The tip's difficulty is the wrong figure whenever the next block's differs: after a retarget, and on test
/// networks, where a block may be mined at the minimum difficulty once 20 minutes pass without one.
/// </summary>
public sealed class NetworkDifficulty(ITemplateRefresher templates, INetworkStatsStore networkStats) : INetworkDifficulty
{
    /// <summary>The difficulty of a test network's occasional easy block, which says nothing about the network.</summary>
    private const double MinimumDifficulty = 1;

    private double _lastRegular;

    public double Current
    {
        get
        {
            var mined = templates.Status.Difficulty;
            if (mined > MinimumDifficulty)
            {
                Volatile.Write(ref _lastRegular, mined.Value);
                return mined.Value;
            }

            // An easy block is on offer (or nothing is known yet): effort and payout windows keep measuring
            // against the network's regular difficulty, when one has been seen.
            var remembered = Volatile.Read(ref _lastRegular);
            return remembered > 0 ? remembered : mined ?? networkStats.Current?.Difficulty ?? 0;
        }
    }
}