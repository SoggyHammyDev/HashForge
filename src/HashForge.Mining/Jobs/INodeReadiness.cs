namespace HashForge.Mining.Jobs;

/// <summary>Answers "may the pool hand out work right now?" and keeps the log quiet while the answer is no.</summary>
public interface INodeReadiness
{
    /// <summary>True when the node has reported its status and is not still downloading the chain.</summary>
    bool IsReady();

    void ReportNotReady(string reason);

    void ReportReady();
}