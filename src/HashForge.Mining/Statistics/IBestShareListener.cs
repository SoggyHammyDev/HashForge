using HashForge.Domain;

namespace HashForge.Mining.Statistics;

/// <summary>Told when a pool records a new best share.</summary>
public interface IBestShareListener
{
    void OnPoolBestShare(ShareResult share);
}
