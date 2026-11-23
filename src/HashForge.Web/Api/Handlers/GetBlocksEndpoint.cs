using HashForge.Mining.History;
using Microsoft.Extensions.Options;

namespace HashForge.Web.Api.Handlers;

/// <summary>GET /api/blocks?count=N — the most recent blocks the pool found, newest first.</summary>
public sealed class GetBlocksEndpoint(IBlockRepository blocks, IOptions<WebOptions> options) : IApiEndpoint
{
    public const int MaxCount = 500;

    public void Map(IEndpointRouteBuilder routes) => routes.MapGet("blocks", HandleAsync);

    public async Task<IResult> HandleAsync(int? count, CancellationToken cancellationToken)
    {
        var requested = count ?? options.Value.RecentBlockCount;
        if (requested is < 1 or > MaxCount)
        {
            return ApiResults.BadRequest($"'count' must be between 1 and {MaxCount}.");
        }

        return ApiResults.Ok(await blocks.GetRecentAsync(requested, cancellationToken).ConfigureAwait(false));
    }
}
