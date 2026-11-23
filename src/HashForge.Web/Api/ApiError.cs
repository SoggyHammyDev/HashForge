namespace HashForge.Web.Api;

/// <summary>One problem with a request. <see cref="Code"/> is stable and machine-readable; <see cref="Message"/> is for people.</summary>
public sealed record ApiError(string Code, string Message)
{
    public const string NotFoundCode = "not_found";
    public const string InvalidRequestCode = "invalid_request";
    public const string InternalErrorCode = "internal_error";
    public const string NodeUnavailableCode = "node_unavailable";
}
