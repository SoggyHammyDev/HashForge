using HashForge.Web.Services;

namespace HashForge.Web.Api.Handlers;

/// <summary>GET /api/pools/{name} — one pool's live statistics.</summary>
public sealed class GetPoolEndpoint(IPoolOverview overview) : IApiEndpoint
{
    public void Map(IEndpointRouteBuilder routes) => routes.MapGet("pools/{name}", Handle);

    public IResult Handle(string name) =>
        overview.FindPool(name) is { } pool
            ? ApiResults.Ok(pool)
            : ApiResults.NotFound($"There is no running pool called '{name}'.");
}
