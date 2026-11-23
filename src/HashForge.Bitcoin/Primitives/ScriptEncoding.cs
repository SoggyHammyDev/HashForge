namespace HashForge.Bitcoin.Primitives;

/// <summary>The few script encodings a coinbase input needs.</summary>
public static class ScriptEncoding
{
    public const int MaxDirectPushLength = 75;

    private const byte OpZero = 0x00;
    private const byte OpOneMinusOne = 0x50;
    private const int MaxSmallInteger = 16;
    private const byte SignBit = 0x80;

    /// <summary>Encodes a block height exactly as Bitcoin Core does for the BIP34 coinbase check.</summary>
    public static byte[] EncodeHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (height == 0)
        {
            return [OpZero];
        }

        if (height <= MaxSmallInteger)
        {
            return [(byte)(OpOneMinusOne + height)];
        }

        return PushData(EncodeNumber(height));
    }

    /// <summary>Prefixes data with the opcode that pushes it onto the stack.</summary>
    public static byte[] PushData(ReadOnlySpan<byte> data)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, MaxDirectPushLength);
        return [(byte)data.Length, .. data];
    }

    private static byte[] EncodeNumber(long value)
    {
        var bytes = new List<byte>();
        while (value > 0)
        {
            bytes.Add((byte)(value & byte.MaxValue));
            value >>= 8;
        }

        // A set top bit would be read as a minus sign, so pad with a zero byte.
        if ((bytes[^1] & SignBit) != 0)
        {
            bytes.Add(0);
        }

        return [.. bytes];
    }
}
