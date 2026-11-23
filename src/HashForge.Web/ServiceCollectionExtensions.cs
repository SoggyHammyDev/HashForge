using System.Net;
using HashForge.Web.Admin;
using HashForge.Web.Api;
using HashForge.Web.Branding;
using HashForge.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HashForge.Web;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHashForgeWeb(this IServiceCollection services)
    {
        services.AddOptions<WebOptions>().BindConfiguration(WebOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AdminOptions>().BindConfiguration(AdminOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdminOptions>, AdminOptionsValidator>();
        services.AddOptions<BrandingOptions>().BindConfiguration(BrandingOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrandingOptions>, BrandingOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IAdminAccessPolicy, AdminAccessPolicy>();
        services.AddSingleton<IPoolOverview, PoolOverview>();
        services.AddSingleton<IConnectedMiners, ConnectedMiners>();
        services.AddHashForgeApi();

        // Behind a reverse proxy every request arrives from the proxy; trusting its forwarded header for the
        // listed proxies only is what lets the admin allow-list see who is really asking.
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<WebOptions>>((forwarded, web) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in web.Value.ConfiguredProxies)
            {
                forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        services.AddSingleton<IValidateOptions<WebOptions>, WebOptionsValidator>();
        return services;
    }
}
