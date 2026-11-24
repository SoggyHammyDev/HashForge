using HashForge.Bitcoin.Notifications;
using HashForge.Bitcoin.Rpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HashForge.Bitcoin.UnitTests;

public sealed class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddHashForgeBitcoin()
            .BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["BitcoinNode:RpcUrl"] = "http://node:8332/",
        ["BitcoinNode:RpcUser"] = "user",
        ["BitcoinNode:RpcPassword"] = "pass",
    };

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_Resolved_THEN_AllRoleInterfacesShareOneClient()
    {
        using var provider = BuildProvider(ValidSettings());

        var client = provider.GetRequiredService<BitcoinNodeClient>();

        Assert.Same(client, provider.GetRequiredService<IBlockTemplateSource>());
        Assert.Same(client, provider.GetRequiredService<IBlockSubmitter>());
        Assert.Same(client, provider.GetRequiredService<IChainInfoSource>());
        Assert.Same(client, provider.GetRequiredService<IChainTipSource>());
        Assert.Same(client, provider.GetRequiredService<IAddressValidator>());
        Assert.Same(client, provider.GetRequiredService<IBlockHeaderSource>());
    }

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_HttpClientCreated_THEN_ItTargetsNodeWithBasicAuth()
    {
        using var provider = BuildProvider(ValidSettings());

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(BitcoinRpcInvoker.HttpClientName);

        Assert.Equal(new Uri("http://node:8332/"), client.BaseAddress);
        Assert.Equal("Basic", client.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("dXNlcjpwYXNz", client.DefaultRequestHeaders.Authorization.Parameter);
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_RequiredSettingMissing_THEN_OptionsValidationFails()
    {
        var settings = ValidSettings();
        settings.Remove("BitcoinNode:RpcPassword");
        using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<BitcoinNodeOptions>>().Value);
    }

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_ZmqAddressConfigured_THEN_BlockNotificationsAreEnabled()
    {
        var settings = ValidSettings();
        settings["BitcoinNode:ZmqBlockUrl"] = "tcp://node:28332";
        using var provider = BuildProvider(settings);

        Assert.True(provider.GetRequiredService<IBlockNotificationSource>().IsEnabled);
    }

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_NoZmqAddressConfigured_THEN_BlockNotificationsAreDisabled()
    {
        using var provider = BuildProvider(ValidSettings());

        Assert.False(provider.GetRequiredService<IBlockNotificationSource>().IsEnabled);
    }

    [Fact]
    public void GIVEN_AddHashForgeBitcoin_WHEN_ZmqAddressIsNotAZeroMqAddress_THEN_OptionsValidationFails()
    {
        var settings = ValidSettings();
        settings["BitcoinNode:ZmqBlockUrl"] = "http://node:28332";
        using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<BitcoinNodeOptions>>().Value);
    }
}