using System.Net;

namespace HashForge.Web.Admin;

/// <summary>Hides the admin area from anyone outside the allowed networks by answering "not found".</summary>
public sealed partial class AdminAccessMiddleware(RequestDelegate next, IAdminAccessPolicy policy, ILogger<AdminAccessMiddleware> logger)
{
    public const string AdminPath = "/admin";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remoteAddress = context.Connection.RemoteIpAddress;
        if (!context.Request.Path.StartsWithSegments(AdminPath, StringComparison.OrdinalIgnoreCase) || policy.IsAllowed(remoteAddress))
        {
            return next(context);
        }

        LogDenied(remoteAddress);
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin page request from {RemoteAddress} refused: address is outside the allowed networks")]
    private partial void LogDenied(IPAddress? remoteAddress);
}
