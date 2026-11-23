namespace HashForge.Domain;

public enum ShareOutcome
{
    Accepted,
    StaleJob,
    Duplicate,
    LowDifficulty,
    Malformed,
}
