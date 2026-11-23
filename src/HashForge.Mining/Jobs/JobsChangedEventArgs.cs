namespace HashForge.Mining.Jobs;

public sealed class JobsChangedEventArgs(bool isNewBlock) : EventArgs
{
    /// <summary>True when the chain tip moved, which makes all earlier work worthless.</summary>
    public bool IsNewBlock { get; } = isNewBlock;
}
