using System.Net.Http.Headers;
using System.Text;
using HashForge.Bitcoin.Notifications;
using HashForge.Bitcoin.Rpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HashForge.Bitcoin;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHashForgeBitcoin(this IServiceCollection services)
    {
        services.AddOptions<BitcoinNodeOptions>()
            .BindConfiguration(BitcoinNodeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(BitcoinRpcInvoker.HttpClientName, ConfigureClient);
        services.AddSingleton<IBitcoinRpcInvoker, BitcoinRpcInvoker>();

        services.AddSingleton<IBlockNotificationSource, ZmqBlockNotificationSource>();
        services.AddSingleton<BitcoinNodeClient>();
        services.AddSingleton<IBlockTemplateSource>(sp => sp.GetRequiredService<BitcoinNodeClient>());
        services.AddSingleton<IBlockSubmitter>(sp => sp.GetRequiredService<BitcoinNodeClient>());
        services.AddSingleton<IChainInfoSource>(sp => sp.GetRequiredService<BitcoinNodeClient>());
        services.AddSingleton<IChainTipSource>(sp => sp.GetRequiredService<BitcoinNodeClient>());
        services.AddSingleton<IAddressValidator>(sp => sp.GetRequiredService<BitcoinNodeClient>());
        return services;
    }

    internal static void ConfigureClient(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<BitcoinNodeOptions>>().Value;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.RpcUser}:{options.RpcPassword}"));
        client.BaseAddress = options.RpcUrl;
        client.Timeout = TimeSpan.FromSeconds(options.RpcTimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}


