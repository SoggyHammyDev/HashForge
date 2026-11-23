using Microsoft.AspNetCore.Http.HttpResults;

namespace HashForge.Web.Api;

/// <summary>Builds results in the standard response shape, so no endpoint invents its own.</summary>
public static class ApiResults
{
    public static Ok<ApiResponse<T>> Ok<T>(T data) => TypedResults.Ok(new ApiResponse<T>(data, []));

    public static JsonHttpResult<ApiResponse<object>> Fail(int statusCode, params ApiError[] errors) =>
        TypedResults.Json(new ApiResponse<object>(null, errors), statusCode: statusCode);

    public static JsonHttpResult<ApiResponse<object>> NotFound(string message) =>
        Fail(StatusCodes.Status404NotFound, new ApiError(ApiError.NotFoundCode, message));

    public static JsonHttpResult<ApiResponse<object>> BadRequest(string message) =>
        Fail(StatusCodes.Status400BadRequest, new ApiError(ApiError.InvalidRequestCode, message));
}
