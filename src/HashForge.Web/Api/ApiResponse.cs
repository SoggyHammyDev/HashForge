namespace HashForge.Web.Api;

/// <summary>
/// The shape of every API response. On success <see cref="Data"/> is set and <see cref="Errors"/> is empty;
/// on failure <see cref="Data"/> is null and <see cref="Errors"/> lists what went wrong.
/// </summary>
public sealed record ApiResponse<T>(T? Data, IReadOnlyList<ApiError> Errors);
