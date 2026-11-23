using HashForge.Mining.Network;

namespace HashForge.Web.Api.Handlers;

/// <summary>GET /api/network — the latest Bitcoin network statistics collected by the background job.</summary>
public sealed class GetNetworkEndpoint(INetworkStatsStore networkStats) : IApiEndpoint
{
    public void Map(IEndpointRouteBuilder routes) => routes.MapGet("network", Handle);

    public IResult Handle() =>
        networkStats.Current is { } stats
            ? ApiResults.Ok(stats)
            : ApiResults.Fail(
                StatusCodes.Status503ServiceUnavailable,
                new ApiError(ApiError.NodeUnavailableCode, "The Bitcoin node has not answered yet."));
}
