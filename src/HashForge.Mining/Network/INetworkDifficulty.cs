namespace HashForge.Mining.Network;

public interface INetworkDifficulty
{
    /// <summary>The difficulty a block must currently meet, or zero until the node has been reached.</summary>
    double Current { get; }
}