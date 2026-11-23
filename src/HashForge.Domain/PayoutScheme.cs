namespace HashForge.Domain;

/// <summary>How a pool splits the block reward between miners.</summary>
public enum PayoutScheme
{
    /// <summary>The miner who finds the block receives the whole reward (minus the pool fee).</summary>
    Solo,

    /// <summary>The reward is split by share difficulty submitted during the current round.</summary>
    Prop,

    /// <summary>The reward is split by share difficulty inside a sliding window of the last N shares.</summary>
    Pplns,

    /// <summary>
    /// The miner who finds the block receives a fixed bonus; the rest is split by share difficulty
    /// submitted during the current round.
    /// </summary>
    FinderBonus,
}
