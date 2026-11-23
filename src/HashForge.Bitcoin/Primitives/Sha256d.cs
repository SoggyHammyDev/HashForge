using System.Security.Cryptography;

namespace HashForge.Bitcoin.Primitives;

/// <summary>Bitcoin's double SHA-256.</summary>
public static class Sha256d
{
    public const int HashSize = 32;

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        Span<byte> first = stackalloc byte[HashSize];
        SHA256.HashData(data, first);
        return SHA256.HashData(first);
    }

    /// <summary>Hashes the concatenation of two 32-byte values, as used for merkle tree nodes.</summary>
    public static byte[] HashPair(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Span<byte> buffer = stackalloc byte[left.Length + right.Length];
        left.CopyTo(buffer);
        right.CopyTo(buffer[left.Length..]);
        return Hash(buffer);
    }
}
