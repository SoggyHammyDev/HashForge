using System.Buffers.Binary;

namespace HashForge.Bitcoin.Primitives;

/// <summary>Builds byte sequences in Bitcoin's wire format.</summary>
public sealed class BitcoinWriter
{
    private const byte VarIntUInt16Marker = 0xfd;
    private const byte VarIntUInt32Marker = 0xfe;

    private readonly List<byte> _buffer = [];

    public BitcoinWriter WriteByte(byte value)
    {
        _buffer.Add(value);
        return this;
    }

    public BitcoinWriter WriteBytes(ReadOnlySpan<byte> value)
    {
        _buffer.AddRange(value);
        return this;
    }

    public BitcoinWriter WriteUInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return WriteBytes(bytes);
    }

    public BitcoinWriter WriteInt64(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return WriteBytes(bytes);
    }

    public BitcoinWriter WriteVarInt(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (value < VarIntUInt16Marker)
        {
            return WriteByte((byte)value);
        }

        if (value <= ushort.MaxValue)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
            return WriteByte(VarIntUInt16Marker).WriteBytes(bytes);
        }

        return WriteByte(VarIntUInt32Marker).WriteUInt32((uint)value);
    }

    /// <summary>Writes a length-prefixed byte array.</summary>
    public BitcoinWriter WriteVarBytes(ReadOnlySpan<byte> value) => WriteVarInt(value.Length).WriteBytes(value);

    public byte[] ToArray() => [.. _buffer];
}
