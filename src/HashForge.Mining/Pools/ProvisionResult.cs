namespace HashForge.Mining.Pools;

public sealed record ProvisionResult(bool Succeeded, string? Error)
{
    public static ProvisionResult Success { get; } = new(true, null);

    public static ProvisionResult Failure(string error) => new(false, error);
}
