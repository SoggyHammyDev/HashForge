using HashForge.Stratum.Handlers;
using HashForge.Stratum.Server;
using HashForge.Stratum.Sessions;
using HashForge.Stratum.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HashForge.Stratum;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Stratum server. Requires the mining engine to be registered as well.</summary>
    public static IServiceCollection AddHashForgeStratum(this IServiceCollection services)
    {
        services.AddOptions<StratumOptions>()
            .BindConfiguration(StratumOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<ISessionRegistry, SessionRegistry>();
        services.AddSingleton<ISessionFactory, SessionFactory>();
        services.AddSingleton<IWorkSender, WorkSender>();
        services.AddSingleton<IConnectionListenerFactory, TcpConnectionListenerFactory>();
        services.AddSingleton<IStratumDispatcher, StratumDispatcher>();
        services.AddSingleton<ISessionRunner, SessionRunner>();

        services.AddSingleton<IStratumMethodHandler, SubscribeHandler>();
        services.AddSingleton<IStratumMethodHandler, ConfigureHandler>();
        services.AddSingleton<IStratumMethodHandler, ExtranonceSubscribeHandler>();
        services.AddSingleton<IStratumMethodHandler, SuggestDifficultyHandler>();
        services.AddSingleton<IStratumMethodHandler, AuthorizeHandler>();
        services.AddSingleton<IStratumMethodHandler, SubmitHandler>();

        services.AddHostedService<JobBroadcaster>();
        services.AddSingleton<StratumServer>();
        services.AddHostedService(sp => sp.GetRequiredService<StratumServer>());
        return services;
    }
}
