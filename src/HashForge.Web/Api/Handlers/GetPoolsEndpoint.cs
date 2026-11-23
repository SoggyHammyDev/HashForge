using HashForge.Mining.Statistics;
using HashForge.Web.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HashForge.Web.Api.Handlers;

/// <summary>GET /api/pools — every running pool with its live statistics.</summary>
public sealed class GetPoolsEndpoint(IPoolOverview overview) : IApiEndpoint
{
    public void Map(IEndpointRouteBuilder routes) => routes.MapGet("pools", Handle);

    public Ok<ApiResponse<IReadOnlyList<PoolSnapshot>>> Handle() => ApiResults.Ok(overview.GetPools());
}
