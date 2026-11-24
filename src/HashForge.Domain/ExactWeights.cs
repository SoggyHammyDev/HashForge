using System.Numerics;

namespace HashForge.Domain;

/// <summary>
/// Turns share weights into whole numbers that keep exactly the same proportions. A reward split with them
/// uses integer arithmetic throughout, so no satoshi is misplaced by floating-point rounding.
/// </summary>
public static class ExactWeights
{
    private const int MantissaBits = 52;
    private const long MantissaMask = (1L << MantissaBits) - 1;
    private const int ExponentMask = 0x7FF;

    /// <summary>The exponent of a double whose stored exponent is zero or one; both mean 2^-1074 per mantissa unit.</summary>
    private const int SmallestExponent = -1074;

    /// <summary>
    /// Every double is a whole number times a power of two. The weights are brought to the smallest such
    /// power among them, which leaves whole numbers in the original proportions.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A weight is negative, infinite or not a number.</exception>
    public static IReadOnlyList<BigInteger> ToIntegers(IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var parts = new (long Mantissa, int Exponent)[weights.Count];
        for (var i = 0; i < weights.Count; i++)
        {
            if (!double.IsFinite(weights[i]) || weights[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weights), weights[i], "A weight must be a finite number that is not negative.");
            }

            parts[i] = Decompose(weights[i]);
        }

        var lowest = parts.Length == 0 ? 0 : parts.Min(p => p.Exponent);
        return [.. parts.Select(p => new BigInteger(p.Mantissa) << (p.Exponent - lowest))];
    }

    /// <summary>Splits a double into the whole number and the power of two that multiply to give exactly its value.</summary>
    private static (long Mantissa, int Exponent) Decompose(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var storedExponent = (int)((bits >> MantissaBits) & ExponentMask);
        var fraction = bits & MantissaMask;

        // A stored exponent of zero marks zero and the very small "subnormal" numbers, which have no hidden leading bit.
        return storedExponent == 0
            ? (fraction, SmallestExponent)
            : (fraction | (1L << MantissaBits), storedExponent - 1 + SmallestExponent);
    }
}
