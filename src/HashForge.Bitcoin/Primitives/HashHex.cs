namespace HashForge.Bitcoin.Primitives;

/// <summary>
/// Converts hashes between the byte order used inside blocks ("internal") and the reversed hex
/// that nodes and block explorers display.
/// </summary>
public static class HashHex
{
    private const int StratumWordHexLength = 8;

    public static byte[] FromDisplayHex(string displayHex)
    {
        var bytes = Convert.FromHexString(displayHex);
        Array.Reverse(bytes);
        return bytes;
    }

    public static string ToDisplayHex(ReadOnlySpan<byte> internalBytes)
    {
        var copy = internalBytes.ToArray();
        Array.Reverse(copy);
        return Convert.ToHexStringLower(copy);
    }

    /// <summary>
    /// Formats a previous-block hash the way Stratum's mining.notify expects it:
    /// the display hex with the order of its 32-bit words reversed.
    /// </summary>
    public static string ToStratumPreviousHash(string displayHex)
    {
        var words = displayHex.Chunk(StratumWordHexLength).Select(w => new string(w)).Reverse();
        return string.Concat(words);
    }
}
