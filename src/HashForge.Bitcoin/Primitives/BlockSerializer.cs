namespace HashForge.Bitcoin.Primitives;

public static class BlockSerializer
{
    public const int HeaderSize = 80;

    public static byte[] SerializeHeader(BlockHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return new BitcoinWriter()
            .WriteUInt32(header.Version)
            .WriteBytes(header.PreviousBlockHash)
            .WriteBytes(header.MerkleRoot)
            .WriteUInt32(header.Time)
            .WriteUInt32(header.Bits)
            .WriteUInt32(header.Nonce)
            .ToArray();
    }

    /// <summary>Serialises a full block: header, then the coinbase, then every other transaction.</summary>
    public static byte[] SerializeBlock(byte[] header, byte[] coinbase, IReadOnlyList<byte[]> otherTransactions)
    {
        ArgumentNullException.ThrowIfNull(otherTransactions);
        var writer = new BitcoinWriter()
            .WriteBytes(header)
            .WriteVarInt(otherTransactions.Count + 1)
            .WriteBytes(coinbase);
        foreach (var transaction in otherTransactions)
        {
            writer.WriteBytes(transaction);
        }

        return writer.ToArray();
    }
}
