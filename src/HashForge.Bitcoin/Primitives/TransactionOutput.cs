namespace HashForge.Bitcoin.Primitives;

public sealed record TransactionOutput(long Satoshis, byte[] ScriptPubKey);
