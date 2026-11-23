using Microsoft.AspNetCore.Diagnostics;

namespace HashForge.Web.Api;

/// <summary>Turns an unexpected failure inside the API into the standard error response.</summary>
public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // By now the request path has been rewritten to the error page, so the original path comes from the feature.
        PathString originalPath = httpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? httpContext.Request.Path.Value;
        if (!originalPath.StartsWithSegments(ApiEndpoints.BasePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        LogApiFailure(exception, originalPath);
        var result = ApiResults.Fail(
            StatusCodes.Status500InternalServerError,
            new ApiError(ApiError.InternalErrorCode, "The request could not be completed. The details are in the pool's log."));
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "API request {Path} failed")]
    private partial void LogApiFailure(Exception exception, string path);
}
