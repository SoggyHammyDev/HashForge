using System.Buffers;
using Microsoft.Extensions.Options;

namespace HashForge.Web;

/// <summary>
/// Lets the pool be served under a sub-path that a reverse proxy picks per request, such as Home Assistant's
/// ingress, which strips its own prefix before forwarding and names it in a header. Reading the prefix back
/// from that header makes every link and script address the pool writes start with it again.
/// </summary>
public sealed class PathBaseHeaderMiddleware(RequestDelegate next, IOptions<WebOptions> options)
{
    private static readonly SearchValues<char> AllowedCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_./");

    private readonly string _header = options.Value.PathBaseHeader.Trim();

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_header.Length > 0 && TryReadPathBase(context.Request.Headers[_header].ToString(), out var pathBase))
        {
            context.Request.PathBase = pathBase;
        }

        return next(context);
    }

    /// <summary>
    /// Accepts a plain absolute path and nothing else. The value ends up in the page's base address, so
    /// anything that could point the browser at another site or folder is refused.
    /// </summary>
    public static bool TryReadPathBase(string? value, out PathString pathBase)
    {
        pathBase = PathString.Empty;
        var trimmed = value?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(trimmed)
            || trimmed[0] != '/'
            || trimmed.AsSpan().ContainsAnyExcept(AllowedCharacters)
            || trimmed.Contains("//", StringComparison.Ordinal)
            || trimmed.Split('/').Any(segment => segment is "." or ".."))
        {
            return false;
        }

        pathBase = new PathString(trimmed);
        return true;
    }
}
