using System.Text.Json.Serialization;
using HashForge.Web.Api.Handlers;

namespace HashForge.Web.Api;

public static class ApiEndpoints
{
    public const string BasePath = "/api";

    public static IServiceCollection AddHashForgeApi(this IServiceCollection services)
    {
        services.AddSingleton<IApiEndpoint, GetPoolsEndpoint>();
        services.AddSingleton<IApiEndpoint, GetPoolEndpoint>();
        services.AddSingleton<IApiEndpoint, GetMinerEndpoint>();
        services.AddSingleton<IApiEndpoint, GetBlocksEndpoint>();
        services.AddSingleton<IApiEndpoint, GetNetworkEndpoint>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    /// <summary>Maps every registered endpoint under /api, plus a catch-all so unknown routes get the standard error shape.</summary>
    public static IEndpointRouteBuilder MapHashForgeApi(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var api = routes.MapGroup(BasePath);
        foreach (var endpoint in routes.ServiceProvider.GetServices<IApiEndpoint>())
        {
            endpoint.Map(api);
        }

        api.MapFallback("{**path}", (string? path) => ApiResults.NotFound($"There is no API endpoint at '{BasePath}/{path}'."));
        return routes;
    }
}
