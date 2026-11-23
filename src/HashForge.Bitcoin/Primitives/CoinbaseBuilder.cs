namespace HashForge.Bitcoin.Primitives;

/// <summary>Builds coinbase transactions split around the extranonce, as Stratum requires.</summary>
public static class CoinbaseBuilder
{
    public const int MaxScriptSigLength = 100;

    private const uint TransactionVersion = 2;
    private const uint FinalSequence = 0xffffffff;
    private const uint LockTime = 0;
    private const int PreviousOutputHashSize = 32;

    public static CoinbaseTransaction Build(CoinbaseSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentOutOfRangeException.ThrowIfLessThan(spec.ExtranonceSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(spec.ExtranonceSize, ScriptEncoding.MaxDirectPushLength);

        var heightScript = ScriptEncoding.EncodeHeight(spec.Height);
        var tagScript = spec.Tag.Length == 0 ? [] : ScriptEncoding.PushData(spec.Tag);
        // One extra byte for the opcode that pushes the extranonce.
        var scriptSigLength = heightScript.Length + 1 + spec.ExtranonceSize + tagScript.Length;
        if (scriptSigLength > MaxScriptSigLength)
        {
            throw new ArgumentException(
                $"Coinbase script is {scriptSigLength} bytes; the consensus limit is {MaxScriptSigLength}.", nameof(spec));
        }

        var prefix = new BitcoinWriter()
            .WriteUInt32(TransactionVersion)
            .WriteVarInt(1)
            .WriteBytes(new byte[PreviousOutputHashSize])
            .WriteUInt32(uint.MaxValue)
            .WriteVarInt(scriptSigLength)
            .WriteBytes(heightScript)
            .WriteByte((byte)spec.ExtranonceSize)
            .ToArray();

        var suffix = new BitcoinWriter().WriteBytes(tagScript).WriteUInt32(FinalSequence);
        WriteOutputs(suffix, spec);
        suffix.WriteUInt32(LockTime);

        return new CoinbaseTransaction(prefix, suffix.ToArray(), spec.ExtranonceSize, spec.WitnessCommitmentScript is not null);
    }

    private static void WriteOutputs(BitcoinWriter writer, CoinbaseSpec spec)
    {
        var outputs = spec.WitnessCommitmentScript is null
            ? spec.Outputs
            : [.. spec.Outputs, new TransactionOutput(0, spec.WitnessCommitmentScript)];

        writer.WriteVarInt(outputs.Count);
        foreach (var output in outputs)
        {
            writer.WriteInt64(output.Satoshis).WriteVarBytes(output.ScriptPubKey);
        }
    }
}
