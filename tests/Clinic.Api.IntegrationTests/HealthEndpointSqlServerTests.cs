using System.Net;
using Clinic.Api.IntegrationTests.Identity;

namespace Clinic.Api.IntegrationTests;

public sealed class HealthEndpointSqlServerTests
    : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public HealthEndpointSqlServerTests(IdentitySqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetReadyHealthReturnsOkWhenDatabaseIsAvailable()
    {
        using HttpClient client = _fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
