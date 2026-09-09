using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Catalog;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Packages;

public sealed class PackageFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 10, 0, 0,
        TimeSpan.Zero);
    private readonly IdentitySqlServerFixture _fixture;
    public PackageFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PackageCatalogEnforcesCompositionAvailabilityConcurrencyAndRoles()
    {
        _fixture.Clock.SetUtcNow(Now);
        using HttpClient admin = _fixture.CreateClient();
        await LoginAsync(admin, "integration.admin", IdentitySqlServerFixture.InitialAdminPassword);
        await SendAsync(admin, HttpMethod.Post, "/api/auth/change-password",
            new ChangePasswordRequest(IdentitySqlServerFixture.InitialAdminPassword,
                IdentitySqlServerFixture.UpdatedAdminPassword), HttpStatusCode.OK);

        DepartmentResponse department = await ReadAsync<DepartmentResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/departments",
            new CreateDepartmentRequest("قسم الباقات", null, "غرفة الباقات"), HttpStatusCode.OK));
        SpecializationResponse specialization = await ReadAsync<SpecializationResponse>(
            await SendAsync(admin, HttpMethod.Post,
                $"/api/admin/departments/{department.Id}/specializations",
                new CreateSpecializationRequest("جلسات الباقات"), HttpStatusCode.OK));
        ServiceResponse service = await ReadAsync<ServiceResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/services", new CreateServiceRequest(department.Id,
                specialization.Id, "جلسة الباقة", ServiceType.Session, 30, PricingMode.Fixed,
                250m), HttpStatusCode.OK));

        PackageResponse package = await ReadAsync<PackageResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/packages", new CreatePackageRequest(department.Id,
                "باقة 12 جلسة", 2400m, 14, 90,
                [new PackageLineRequest(service.Id, 12, 250m)]),
            HttpStatusCode.Created));
        Assert.True(package.IsAvailable);
        Assert.Equal(12, package.SessionCount);

        using HttpResponseMessage activePackageBlocksServiceArchive = await SendAsync(admin,
            HttpMethod.Post, $"/api/admin/services/{service.Id}/archive",
            new RowVersionRequest(service.RowVersion), HttpStatusCode.Conflict);

        PageResponse<PackageResponse> available = await ReadAsync<PageResponse<PackageResponse>>(
            await admin.GetAsync("/api/catalog/packages?pageNumber=1&pageSize=20"));
        Assert.Contains(available.Items, item => item.Id == package.Id);

        PackageResponse disabled = await ReadAsync<PackageResponse>(await SendAsync(admin,
            HttpMethod.Put, $"/api/admin/packages/{package.Id}/activation",
            new ActivationRequest(false, package.RowVersion), HttpStatusCode.OK));
        Assert.False(disabled.IsAvailable);

        using HttpResponseMessage stale = await SendAsync(admin, HttpMethod.Put,
            $"/api/admin/packages/{package.Id}", new UpdatePackageRequest("اسم جديد", 2200m,
                14, 90, [new PackageLineRequest(service.Id, 12, 250m)], package.RowVersion),
            HttpStatusCode.Conflict);

        PageResponse<PackageResponse> hidden = await ReadAsync<PageResponse<PackageResponse>>(
            await admin.GetAsync("/api/catalog/packages?pageNumber=1&pageSize=20"));
        Assert.DoesNotContain(hidden.Items, item => item.Id == package.Id);

        using HttpResponseMessage archivedService = await SendAsync(admin, HttpMethod.Post,
            $"/api/admin/services/{service.Id}/archive",
            new RowVersionRequest(service.RowVersion), HttpStatusCode.NoContent);

        await VerifyStoppedDepartmentPackageIsUnavailableAsync(admin);
        await VerifySecretaryCanOnlyReadAsync(admin, package.Id);
        await VerifyDatabaseConstraintsAsync(package.Id, department.Id, service.Id);

        using HttpClient anonymous = _fixture.CreateClient();
        using HttpResponseMessage unauthorized = await anonymous.GetAsync("/api/catalog/packages");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    private static async Task VerifyStoppedDepartmentPackageIsUnavailableAsync(HttpClient admin)
    {
        DepartmentResponse department = await ReadAsync<DepartmentResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/departments",
            new CreateDepartmentRequest("قسم متوقف", null, "غرفة متوقفة"), HttpStatusCode.OK));
        SpecializationResponse specialization = await ReadAsync<SpecializationResponse>(
            await SendAsync(admin, HttpMethod.Post,
                $"/api/admin/departments/{department.Id}/specializations",
                new CreateSpecializationRequest("تخصص متوقف"), HttpStatusCode.OK));
        ServiceResponse service = await ReadAsync<ServiceResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/services", new CreateServiceRequest(department.Id,
                specialization.Id, "خدمة متوقفة", ServiceType.Session, 30, PricingMode.Fixed,
                100m), HttpStatusCode.OK));
        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/admin/scheduling/departments/{department.Id}/closures",
            new CreateClosureRequest(Now.AddHours(-1), Now.AddHours(1), "توقف اختباري"),
            HttpStatusCode.OK);

        PackageResponse package = await ReadAsync<PackageResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/packages", new CreatePackageRequest(department.Id,
                "باقة قسم متوقف", 100m, 14, 90,
                [new PackageLineRequest(service.Id, 1, 100m)]),
            HttpStatusCode.Created));

        Assert.False(package.IsAvailable);
        Assert.Equal("القسم متوقف مؤقتًا.", package.UnavailabilityReason);
        PageResponse<PackageResponse> available = await ReadAsync<PageResponse<PackageResponse>>(
            await admin.GetAsync($"/api/catalog/packages?departmentId={department.Id}"));
        Assert.DoesNotContain(available.Items, item => item.Id == package.Id);
    }

    private async Task VerifySecretaryCanOnlyReadAsync(HttpClient admin, long packageId)
    {
        _ = await SendAsync(admin, HttpMethod.Post, "/api/admin/secretaries",
            new CreateSecretaryRequest("سكرتيرة الباقات", "01044444444", null,
                "packages.secretary", "SecretaryPass1"), HttpStatusCode.OK);
        using HttpClient secretary = _fixture.CreateClient();
        await LoginAsync(secretary, "packages.secretary", "SecretaryPass1");
        _ = await SendAsync(secretary, HttpMethod.Post, "/api/auth/change-password",
            new ChangePasswordRequest("SecretaryPass1", "SecretaryPass2"), HttpStatusCode.OK);

        using HttpResponseMessage read = await secretary.GetAsync(
            "/api/catalog/packages?pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using HttpResponseMessage write = await SendAsync(secretary, HttpMethod.Post,
            $"/api/admin/packages/{packageId}/archive", new RowVersionRequest(
                Convert.ToBase64String(new byte[8])), HttpStatusCode.Forbidden);
    }

    private async Task VerifyDatabaseConstraintsAsync(long packageId, long departmentId,
        long serviceId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [packages].[PackageServices] ([PackageId], [DepartmentId], [ServiceId], [SessionsIncluded], [UnitPriceAtDefinition], [IsActive]) VALUES ({packageId}, {departmentId + 9999}, {serviceId}, {1}, {100m}, {true})"));
        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [packages].[PackageServices] ([PackageId], [DepartmentId], [ServiceId], [SessionsIncluded], [UnitPriceAtDefinition], [IsActive]) VALUES ({packageId}, {departmentId}, {serviceId}, {1}, {100m}, {true})"));
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password) =>
        _ = await SendAsync(client, HttpMethod.Post, "/api/auth/login",
            new LoginRequest(userName, password), HttpStatusCode.OK);

    private static async Task<HttpResponseMessage> SendAsync<T>(HttpClient client,
        HttpMethod method, string uri, T body, HttpStatusCode expected)
    {
        string token = await ReadAsync<CsrfResponse>(await client.GetAsync("/api/auth/csrf")) is
            { Token: var value } ? value : string.Empty;
        HttpRequestMessage request = new(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        HttpResponseMessage response = await client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected} from {uri}, got {(int)response.StatusCode}: {content}");
        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>() ??
        throw new InvalidOperationException("The API response was empty.");

    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CreateDepartmentRequest(string Name, string? Description, string RoomName);
    private sealed record CreateSpecializationRequest(string Name);
    private sealed record CreateSecretaryRequest(string FullName, string PhoneNumber,
        string? Email, string UserName, string TemporaryPassword);
    private sealed record CreateServiceRequest(long DepartmentId, long SpecializationId,
        string Name, ServiceType ServiceType, int DurationMinutes, PricingMode PricingMode,
        decimal UnitPrice);
    private sealed record CreateClosureRequest(DateTimeOffset StartAt, DateTimeOffset EndAt,
        string Reason);
    private sealed record CreatePackageRequest(long DepartmentId, string Name, decimal BasePrice,
        int ActivationGraceDays, int UsageDurationDays,
        IReadOnlyCollection<PackageLineRequest> Services);
    private sealed record UpdatePackageRequest(string Name, decimal BasePrice,
        int ActivationGraceDays, int UsageDurationDays,
        IReadOnlyCollection<PackageLineRequest> Services, string RowVersion);
    private sealed record PackageLineRequest(long ServiceId, int SessionsIncluded,
        decimal UnitPriceAtDefinition);
    private sealed record ActivationRequest(bool IsActive, string RowVersion);
    private sealed record RowVersionRequest(string RowVersion);
    private sealed record DepartmentResponse(long Id);
    private sealed record SpecializationResponse(long Id);
    private sealed record ServiceResponse(long Id, string RowVersion);
    private sealed record PackageResponse(long Id, int SessionCount, bool IsAvailable,
        string? UnavailabilityReason, string RowVersion);
    private sealed record PageResponse<T>(IReadOnlyCollection<T> Items);
}
