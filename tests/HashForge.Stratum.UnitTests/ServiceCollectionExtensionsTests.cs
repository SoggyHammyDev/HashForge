using HashForge.Mining;
using HashForge.Mining.Jobs;
using HashForge.Mining.Payouts;
using HashForge.Mining.Pools;
using HashForge.Mining.Shares;
using HashForge.Stratum.Handlers;
using HashForge.Stratum.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.UnitTests;

public sealed class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddOptions()
            .AddSingleton(Options.Create(new MiningOptions()))
            .AddSingleton(Substitute.For<IPoolRegistry>())
            .AddSingleton(Substitute.For<IJobSource>())
            .AddSingleton(Substitute.For<IShareProcessor>())
            .AddSingleton(Substitute.For<IAddressScriptResolver>())
            .AddHashForgeStratum()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    [Fact]
    public void GIVEN_AddHashForgeStratum_WHEN_ContainerIsBuilt_THEN_EveryStratumMethodHasOneHandler()
    {
        using var provider = BuildProvider();

        var methods = provider.GetServices<IStratumMethodHandler>().Select(h => h.Method).Order();

        Assert.Equal(
            ["mining.authorize", "mining.configure", "mining.extranonce.subscribe", "mining.submit", "mining.subscribe", "mining.suggest_difficulty"],
            methods);
        Assert.NotNull(provider.GetRequiredService<IStratumDispatcher>());
    }

    [Fact]
    public void GIVEN_AddHashForgeStratum_WHEN_Resolved_THEN_ServerIsSharedBetweenHostAndConsumers()
    {
        using var provider = BuildProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();

        Assert.Equal(2, hosted.Count);
        Assert.Contains(provider.GetRequiredService<StratumServer>(), hosted);
    }

    [Fact]
    public void GIVEN_AddHashForgeStratum_WHEN_ConfigurationIsInvalid_THEN_OptionsValidationFails()
    {
        using var provider = BuildProvider(new() { ["Stratum:TargetSharesPerMinute"] = "0" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<StratumOptions>>().Value);
    }

    [Fact]
    public void GIVEN_AddHashForgeStratum_WHEN_ConfigurationProvided_THEN_OptionsAreBound()
    {
        using var provider = BuildProvider(new() { ["Stratum:InitialDifficulty"] = "2048" });

        Assert.Equal(2048, provider.GetRequiredService<IOptions<StratumOptions>>().Value.InitialDifficulty);
    }
}
