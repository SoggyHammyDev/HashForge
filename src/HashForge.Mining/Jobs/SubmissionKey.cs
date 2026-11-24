using System.Buffers.Binary;

namespace HashForge.Mining.Jobs;

/// <summary>
/// A block header hash held as four numbers. Every accepted share is remembered until the next block so that
/// it cannot be credited twice, which on a busy pool is over a million entries; this takes 32 bytes where the
/// same hash written out as text took about 150.
/// </summary>
public readonly record struct SubmissionKey(ulong First, ulong Second, ulong Third, ulong Fourth)
{
    public const int HashLength = 32;

    private const int PartLength = sizeof(ulong);

    /// <exception cref="ArgumentException">The hash is not 32 bytes long.</exception>
    public static SubmissionKey FromHash(ReadOnlySpan<byte> hash)
    {
        if (hash.Length != HashLength)
        {
            throw new ArgumentException($"A header hash is {HashLength} bytes, not {hash.Length}.", nameof(hash));
        }

        return new SubmissionKey(
            BinaryPrimitives.ReadUInt64LittleEndian(hash),
            BinaryPrimitives.ReadUInt64LittleEndian(hash[PartLength..]),
            BinaryPrimitives.ReadUInt64LittleEndian(hash[(PartLength * 2)..]),
            BinaryPrimitives.ReadUInt64LittleEndian(hash[(PartLength * 3)..]));
    }
}
