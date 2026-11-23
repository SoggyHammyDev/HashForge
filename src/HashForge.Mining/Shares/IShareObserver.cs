using HashForge.Domain;

namespace HashForge.Mining.Shares;

/// <summary>Told about every share, accepted or rejected.</summary>
public interface IShareObserver
{
    void OnShare(ShareResult share);
}
