using System.Net;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Api.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthReturnsOk()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLiveHealthDoesNotRequireDatabaseAndAddsSecurityHeaders()
    {
        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        Guid correlationId = Guid.NewGuid();
        using HttpRequestMessage request = new(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", correlationId.ToString());

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlationId.ToString("N"),
            Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("nosniff",
            Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY",
            Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer",
            Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task GetReadyHealthReturnsServiceUnavailableWhenDatabaseIsUnavailable()
    {
        const string unavailableConnection =
            "Server=127.0.0.1,1;Database=ClinicUnavailable;User Id=invalid;" +
            "Password=invalid;Encrypt=False;TrustServerCertificate=True;" +
            "Connect Timeout=1;ConnectRetryCount=0";
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ClinicDbContext>>();
                services.RemoveAll<ClinicDbContext>();
                services.AddDbContext<ClinicDbContext>(options =>
                    options.UseSqlServer(unavailableConnection));
            }));
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
