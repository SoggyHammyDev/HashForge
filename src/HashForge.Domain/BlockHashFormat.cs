using System.Text.RegularExpressions;

namespace HashForge.Domain;

/// <summary>A shape check for text that claims to be a block hash, used to reject junk from public input.</summary>
/// <remarks>Hashes are written in lowercase everywhere in the pool, so only lowercase is accepted.</remarks>
public static partial class BlockHashFormat
{
    public static bool IsValid(string? hash) => hash is not null && Shape().IsMatch(hash);

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Shape();
}
