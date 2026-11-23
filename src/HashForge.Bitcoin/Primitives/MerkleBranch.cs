namespace HashForge.Bitcoin.Primitives;

/// <summary>Merkle maths for the coinbase transaction, which always sits at index zero.</summary>
public static class MerkleBranch
{
    /// <summary>
    /// Builds the hashes needed to climb from the coinbase to the merkle root,
    /// given the ids of every other transaction in the block (internal byte order).
    /// </summary>
    public static IReadOnlyList<byte[]> Build(IReadOnlyList<byte[]> otherTransactionIds)
    {
        ArgumentNullException.ThrowIfNull(otherTransactionIds);

        var branch = new List<byte[]>();
        // Index zero stands in for the coinbase, whose hash is not known yet.
        var level = new List<byte[]?> { null };
        level.AddRange(otherTransactionIds);

        while (level.Count > 1)
        {
            if (level.Count % 2 != 0)
            {
                level.Add(level[^1]);
            }

            branch.Add(level[1]!);
            var next = new List<byte[]?> { null };
            for (var i = 2; i < level.Count; i += 2)
            {
                next.Add(Sha256d.HashPair(level[i]!, level[i + 1]!));
            }

            level = next;
        }

        return branch;
    }

    public static byte[] ComputeRoot(byte[] coinbaseTransactionId, IEnumerable<byte[]> branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        return branch.Aggregate(coinbaseTransactionId, (current, sibling) => Sha256d.HashPair(current, sibling));
    }
}
