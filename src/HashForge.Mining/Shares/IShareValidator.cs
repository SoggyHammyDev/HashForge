using HashForge.Mining.Jobs;

namespace HashForge.Mining.Shares;

public interface IShareValidator
{
    ShareValidation Validate(ShareSubmission submission, MiningJob job);
}
