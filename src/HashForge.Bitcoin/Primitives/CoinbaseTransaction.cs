namespace HashForge.Bitcoin.Primitives;

/// <summary>A coinbase transaction with a gap where each miner's extranonce goes.</summary>
public sealed class CoinbaseTransaction(byte[] prefix, byte[] suffix, int extranonceSize, bool hasWitnessCommitment)
{
    private const int VersionSize = 4;
    private const int LockTimeSize = 4;
    private const int WitnessReservedValueSize = 32;

    private static readonly byte[] SegwitMarkerAndFlag = [0x00, 0x01];

    /// <summary>The bytes before the extranonce ("coinb1" in Stratum).</summary>
    public byte[] Prefix { get; } = prefix;

    /// <summary>The bytes after the extranonce ("coinb2" in Stratum).</summary>
    public byte[] Suffix { get; } = suffix;

    public int ExtranonceSize { get; } = extranonceSize;

    /// <summary>Serialises without witness data; hashing this gives the transaction id.</summary>
    public byte[] Assemble(ReadOnlySpan<byte> extranonce)
    {
        if (extranonce.Length != ExtranonceSize)
        {
            throw new ArgumentException($"Extranonce must be {ExtranonceSize} bytes.", nameof(extranonce));
        }

        return [.. Prefix, .. extranonce, .. Suffix];
    }

    public byte[] ComputeTransactionId(ReadOnlySpan<byte> extranonce) => Sha256d.Hash(Assemble(extranonce));

    /// <summary>Serialises for inclusion in a block, adding the witness reserved value when required.</summary>
    public byte[] AssembleForBlock(ReadOnlySpan<byte> extranonce)
    {
        var legacy = Assemble(extranonce);
        if (!hasWitnessCommitment)
        {
            return legacy;
        }

        return new BitcoinWriter()
            .WriteBytes(legacy.AsSpan(0, VersionSize))
            .WriteBytes(SegwitMarkerAndFlag)
            .WriteBytes(legacy.AsSpan(VersionSize, legacy.Length - VersionSize - LockTimeSize))
            .WriteVarInt(1)
            .WriteVarBytes(new byte[WitnessReservedValueSize])
            .WriteBytes(legacy.AsSpan(legacy.Length - LockTimeSize))
            .ToArray();
    }
}
