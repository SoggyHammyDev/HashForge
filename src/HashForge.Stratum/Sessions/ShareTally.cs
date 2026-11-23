namespace HashForge.Stratum.Sessions;

/// <summary>Counts the shares one connection has had accepted and rejected.</summary>
public sealed class ShareTally
{
    private long _accepted;
    private long _rejected;

    public long Accepted => Interlocked.Read(ref _accepted);

    public long Rejected => Interlocked.Read(ref _rejected);

    public void Count(bool accepted)
    {
        if (accepted)
        {
            Interlocked.Increment(ref _accepted);
        }
        else
        {
            Interlocked.Increment(ref _rejected);
        }
    }
}