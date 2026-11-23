using System.Text.RegularExpressions;

namespace HashForge.Domain;

/// <summary>
/// A cheap shape check for text that claims to be a Bitcoin address, used to reject junk from public
/// input before it is looked up or echoed back. It is not proof of validity; only the node decides that.
/// </summary>
public static partial class AddressFormat
{
    /// <summary>The longest address any current format allows (bech32/bech32m).</summary>
    public const int MaxLength = 90;

    public const int MinLength = 14;

    public static bool IsPlausible(string? address) => address is not null && Shape().IsMatch(address);

    // Every address format (base58, bech32, bech32m) uses only ASCII letters and digits.
    [GeneratedRegex("^[A-Za-z0-9]{14,90}$")]
    private static partial Regex Shape();
}
