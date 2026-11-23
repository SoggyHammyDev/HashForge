using System.Net;
using HashForge.Web.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Web.UnitTests;

public sealed class AdminAccessTests
{
    private static AdminAccessPolicy Policy(params string[] networks) =>
        new(Options.Create(new AdminOptions { AllowedNetworks = [.. networks] }));

    private static async Task<(int Status, bool ReachedPage)> RequestAsync(string path, bool allowed, ILogger<AdminAccessMiddleware>? logger = null)
    {
        var policy = Substitute.For<IAdminAccessPolicy>();
        policy.IsAllowed(Arg.Any<IPAddress?>()).Returns(allowed);
        var reachedPage = false;
        var middleware = new AdminAccessMiddleware(
            _ =>
            {
                reachedPage = true;
                return Task.CompletedTask;
            },
            policy,
            logger ?? Substitute.For<ILogger<AdminAccessMiddleware>>());
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, reachedPage);
    }

    [Theory]
    [InlineData("192.168.0.1", true)]
    [InlineData("192.168.0.254", true)]
    [InlineData("192.168.1.77", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:192.168.1.77", true)]
    [InlineData("192.168.2.1", false)]
    [InlineData("10.0.0.5", false)]
    [InlineData("203.0.113.9", false)]
    [InlineData("2001:db8::1", false)]
    public void GIVEN_IsAllowed_WHEN_NoNetworksConfigured_THEN_DefaultHomeRangesAndLoopbackApply(string address, bool expected)
    {
        Assert.Equal(expected, Policy().IsAllowed(IPAddress.Parse(address)));
    }

    [Fact]
    public void GIVEN_IsAllowed_WHEN_NetworksConfigured_THEN_TheyReplaceTheDefaults()
    {
        var policy = Policy("10.20.0.0/16");

        Assert.True(policy.IsAllowed(IPAddress.Parse("10.20.30.40")));
        Assert.False(policy.IsAllowed(IPAddress.Parse("192.168.0.10")));
        Assert.False(policy.IsAllowed(IPAddress.Loopback));
    }

    [Fact]
    public void GIVEN_IsAllowed_WHEN_AddressIsUnknown_THEN_AccessIsDenied()
    {
        Assert.False(Policy().IsAllowed(null));
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/Admin")]
    [InlineData("/admin/anything")]
    public async Task GIVEN_InvokeAsync_WHEN_AdminPathRequestedFromDisallowedAddress_THEN_RespondsNotFound(string path)
    {
        var (status, reachedPage) = await RequestAsync(path, allowed: false);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.False(reachedPage);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_AccessIsRefused_THEN_AddressIsLogged()
    {
        var logger = Substitute.For<ILogger<AdminAccessMiddleware>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        await RequestAsync("/admin", allowed: false, logger);

        var call = Assert.Single(logger.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, call.GetArguments()[0]);
        Assert.Contains("203.0.113.9", call.GetArguments()[2]!.ToString());
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_AdminPathRequestedFromAllowedAddress_THEN_RequestContinues()
    {
        var (status, reachedPage) = await RequestAsync("/admin", allowed: true);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(reachedPage);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/pool/solo")]
    [InlineData("/administrator")]
    public async Task GIVEN_InvokeAsync_WHEN_PublicPathRequestedFromAnyAddress_THEN_RequestContinues(string path)
    {
        var (_, reachedPage) = await RequestAsync(path, allowed: false);

        Assert.True(reachedPage);
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_NetworksAreValidOrEmpty_THEN_Succeeds()
    {
        var validator = new AdminOptionsValidator();

        Assert.True(validator.Validate(null, new AdminOptions()).Succeeded);
        Assert.True(validator.Validate(null, new AdminOptions { AllowedNetworks = ["10.0.0.0/8", "fd00::/8"] }).Succeeded);
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_NetworkIsNotCidr_THEN_FailsNamingIt()
    {
        var result = new AdminOptionsValidator().Validate(null, new AdminOptions { AllowedNetworks = ["192.168.0.x"] });

        Assert.True(result.Failed);
        Assert.Contains("192.168.0.x", result.FailureMessage);
    }
}
