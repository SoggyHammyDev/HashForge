using Bunit;
using HashForge.Web.Components.Layout;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HashForge.Web.UnitTests;

public sealed class PathBaseHeaderTests : WebTestContext
{
    private const string IngressHeader = "X-Ingress-Path";

    private static async Task<HttpContext> RequestAsync(string configuredHeader, params (string Name, string Value)[] headers)
    {
        var middleware = new PathBaseHeaderMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new WebOptions { PathBaseHeader = configuredHeader }));
        var context = new DefaultHttpContext();
        context.Request.Path = "/pool/solo";
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        await middleware.InvokeAsync(context);

        return context;
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_ConfiguredHeaderNamesASubPath_THEN_ItBecomesThePathBase()
    {
        var context = await RequestAsync(IngressHeader, (IngressHeader, "/api/hassio_ingress/AbC-123_x"));

        Assert.Equal("/api/hassio_ingress/AbC-123_x", context.Request.PathBase.Value);
        Assert.Equal("/pool/solo", context.Request.Path.Value);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_NoHeaderIsConfigured_THEN_HeaderIsIgnored()
    {
        var context = await RequestAsync(string.Empty, (IngressHeader, "/api/hassio_ingress/abc"));

        Assert.False(context.Request.PathBase.HasValue);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_RequestHasNoSuchHeader_THEN_PathBaseStaysEmpty()
    {
        var context = await RequestAsync(IngressHeader, ("X-Forwarded-Prefix", "/elsewhere"));

        Assert.False(context.Request.PathBase.HasValue);
    }

    [Fact]
    public async Task GIVEN_InvokeAsync_WHEN_HeaderIsNotAPlainPath_THEN_RequestContinuesWithoutIt()
    {
        var reachedPage = false;
        var middleware = new PathBaseHeaderMiddleware(
            _ =>
            {
                reachedPage = true;
                return Task.CompletedTask;
            },
            Options.Create(new WebOptions { PathBaseHeader = IngressHeader }));
        var context = new DefaultHttpContext();
        context.Request.Headers[IngressHeader] = "//evil.example";

        await middleware.InvokeAsync(context);

        Assert.True(reachedPage);
        Assert.False(context.Request.PathBase.HasValue);
    }

    [Theory]
    [InlineData("/pool", "/pool")]
    [InlineData("/pool/", "/pool")]
    [InlineData(" /api/hassio_ingress/token ", "/api/hassio_ingress/token")]
    [InlineData("/a.b/c-d_e", "/a.b/c-d_e")]
    public void GIVEN_TryReadPathBase_WHEN_ValueIsAPlainAbsolutePath_THEN_ItIsAcceptedWithoutATrailingSlash(string value, string expected)
    {
        Assert.True(PathBaseHeaderMiddleware.TryReadPathBase(value, out var pathBase));
        Assert.Equal(expected, pathBase.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("pool")]
    [InlineData("//evil.example")]
    [InlineData("/pool//admin")]
    [InlineData("https://evil.example/pool")]
    [InlineData("/pool/../admin")]
    [InlineData("/./pool")]
    [InlineData("/pool?x=1")]
    [InlineData("/pool#top")]
    [InlineData("/po ol")]
    [InlineData("/pool\"><script>")]
    [InlineData("/pool\\admin")]
    [InlineData("/pool%2e%2e")]
    public void GIVEN_TryReadPathBase_WHEN_ValueIsNotAPlainAbsolutePath_THEN_ItIsRefused(string? value)
    {
        Assert.False(PathBaseHeaderMiddleware.TryReadPathBase(value, out var pathBase));
        Assert.False(pathBase.HasValue);
    }

    [Fact]
    public void GIVEN_BaseHref_WHEN_ServedFromTheRoot_THEN_BaseAddressIsTheRoot()
    {
        var page = Render<BaseHref>(parameters => parameters.AddCascadingValue<HttpContext>(new DefaultHttpContext()));

        Assert.Equal("/", page.Find("base").GetAttribute("href"));
    }

    [Fact]
    public void GIVEN_BaseHref_WHEN_ServedUnderASubPath_THEN_BaseAddressIsThatSubPath()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/api/hassio_ingress/token";

        var page = Render<BaseHref>(parameters => parameters.AddCascadingValue<HttpContext>(context));

        Assert.Equal("/api/hassio_ingress/token/", page.Find("base").GetAttribute("href"));
    }

    [Fact]
    public void GIVEN_BaseHref_WHEN_ThereIsNoRequest_THEN_BaseAddressIsTheRoot()
    {
        Assert.Equal("/", Render<BaseHref>().Find("base").GetAttribute("href"));
    }
}
