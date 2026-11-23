namespace HashForge.Domain;

public static class PayoutSchemeExtensions
{
    /// <summary>The short name shown to people, e.g. on badges and in notifications.</summary>
    /// <summary>
    /// True when each miner works towards a block of their own, so a found block ends only its finder's round.
    /// </summary>
    public static bool HasPerMinerRounds(this PayoutScheme scheme) => scheme == PayoutScheme.Solo;

    public static string Label(this PayoutScheme scheme) => scheme switch
    {
        PayoutScheme.FinderBonus => "BONUS",
        _ => scheme.ToString().ToUpperInvariant(),
    };
}
