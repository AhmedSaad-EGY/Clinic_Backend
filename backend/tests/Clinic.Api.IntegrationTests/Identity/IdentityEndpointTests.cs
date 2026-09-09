using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Clinic.Api.IntegrationTests.Identity;

public sealed class IdentityEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdentityEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCsrfReturnsTokenAndPreventsCaching()
    {
        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage response = await client.GetAsync("/api/auth/csrf");
        CsrfResponse? payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("clinic.xsrf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListSecretariesWithoutAuthenticationReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage response = await client.GetAsync("/api/admin/secretaries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ApplicationCookieKeepsIdentitySecurityStampValidation()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IOptionsMonitor<CookieAuthenticationOptions> optionsMonitor =
            scope.ServiceProvider.GetRequiredService<
                IOptionsMonitor<CookieAuthenticationOptions>>();

        CookieAuthenticationOptions options = optionsMonitor.Get(
            IdentityConstants.ApplicationScheme);

        Assert.NotNull(options.Events.OnValidatePrincipal);
        Assert.Equal(
            nameof(SecurityStampValidator.ValidatePrincipalAsync),
            options.Events.OnValidatePrincipal.Method.Name);
        Assert.Equal(
            typeof(SecurityStampValidator),
            options.Events.OnValidatePrincipal.Method.DeclaringType);
    }

    private sealed record CsrfResponse(string Token);
}
