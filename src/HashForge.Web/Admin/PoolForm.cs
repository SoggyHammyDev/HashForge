using HashForge.Domain;
using HashForge.Domain.Formatting;

namespace HashForge.Web.Admin;

/// <summary>The editable fields of the admin page's "start a pool" form.</summary>
public sealed class PoolForm
{
    public string Name { get; set; } = string.Empty;

    public PayoutScheme Scheme { get; set; } = PayoutScheme.Pplns;

    public int Port { get; set; }

    public decimal FeePercent { get; set; } = 1;

    public string FeeAddress { get; set; } = string.Empty;

    /// <summary>Only shown, and only used, when the scheme is the finder bonus.</summary>
    public decimal FinderBonusBitcoin { get; set; } = (decimal)PoolDefinition.DefaultFinderBonusSatoshis / Display.SatoshisPerBitcoin;

    public PoolDefinition ToDefinition() => new()
    {
        Name = (Name ?? string.Empty).Trim(),
        Scheme = Scheme,
        Port = Port,
        FeePercent = FeePercent,
        FeeAddress = (FeeAddress ?? string.Empty).Trim(),
        FinderBonusSatoshis = Scheme == PayoutScheme.FinderBonus
            ? (long)Math.Round(FinderBonusBitcoin * Display.SatoshisPerBitcoin)
            : PoolDefinition.DefaultFinderBonusSatoshis,
    };
}