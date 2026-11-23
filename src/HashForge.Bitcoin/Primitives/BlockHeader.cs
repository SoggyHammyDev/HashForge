namespace HashForge.Bitcoin.Primitives;

/// <summary>The six fields of a block header. Hashes are in internal byte order.</summary>
public sealed record BlockHeader(uint Version, byte[] PreviousBlockHash, byte[] MerkleRoot, uint Time, uint Bits, uint Nonce);
