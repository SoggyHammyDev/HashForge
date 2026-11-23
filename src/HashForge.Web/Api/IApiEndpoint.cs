namespace HashForge.Web.Api;

/// <summary>One API endpoint: its route and the code that answers it. Each lives in its own small class.</summary>
public interface IApiEndpoint
{
    void Map(IEndpointRouteBuilder routes);
}
