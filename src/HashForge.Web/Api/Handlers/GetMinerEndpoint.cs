using HashForge.Domain;
using HashForge.Mining.Statistics;

namespace HashForge.Web.Api.Handlers;

/// <summary>GET /api/pools/{name}/miners/{address} — one miner's statistics, with a breakdown per worker.</summary>
public sealed class GetMinerEndpoint(IStatisticsReader statistics) : IApiEndpoint
{
    public void Map(IEndpointRouteBuilder routes) => routes.MapGet("pools/{name}/miners/{address}", Handle);

    public IResult Handle(string name, string address)
    {
        if (!AddressFormat.IsPlausible(address))
        {
            return ApiResults.BadRequest("The address is not valid. Addresses contain only letters and digits.");
        }

        return statistics.GetMiner(name, address) is { } miner
            ? ApiResults.Ok(miner)
            : ApiResults.NotFound("That address has not submitted any shares to this pool.");
    }
}
