using System.Globalization;
using System.Numerics;

namespace HashForge.Domain;

/// <summary>Conversions between hashes, targets and difficulty.</summary>
public static class DifficultyMath
{
    private const int CompactMantissaBytes = 3;
    private const uint CompactMantissaMask = 0x007fffff;

    /// <summary>The target that corresponds to difficulty 1.</summary>
    public static readonly BigInteger Diff1Target = BigInteger.Parse(
        "00000000FFFF0000000000000000000000000000000000000000000000000000",
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

    /// <summary>Interprets a 32-byte hash (internal, little-endian order) as an unsigned integer.</summary>
    public static BigInteger HashToInteger(ReadOnlySpan<byte> hash) =>
        new(hash, isUnsigned: true, isBigEndian: false);

    public static double DifficultyFromHash(ReadOnlySpan<byte> hash) =>
        DifficultyFromTarget(HashToInteger(hash));

    public static double DifficultyFromTarget(BigInteger target) =>
        target.IsZero ? double.PositiveInfinity : (double)Diff1Target / (double)target;

    public static BigInteger TargetFromDifficulty(double difficulty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(difficulty);
        return new BigInteger((double)Diff1Target / difficulty);
    }

    /// <summary>Expands the compact "nBits" representation used in block headers.</summary>
    public static BigInteger TargetFromCompact(uint bits)
    {
        var exponent = (int)(bits >> 24);
        BigInteger mantissa = bits & CompactMantissaMask;
        var shiftBits = 8 * (exponent - CompactMantissaBytes);
        return shiftBits >= 0 ? mantissa << shiftBits : mantissa >> -shiftBits;
    }
}
