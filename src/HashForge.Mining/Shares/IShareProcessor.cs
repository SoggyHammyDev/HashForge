using HashForge.Domain;

namespace HashForge.Mining.Shares;

public interface IShareProcessor
{
    Task<ShareResult> ProcessAsync(ShareSubmission submission, CancellationToken cancellationToken);
}
