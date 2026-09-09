using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Catalog;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Catalog;

public sealed class CatalogFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public CatalogFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CatalogLifecycleEnforcesRelationshipsPricesConcurrencyAndRoles()
    {
        using HttpClient adminClient = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(
            adminClient,
            "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);

        DepartmentResponse physiotherapy = await CreateDepartmentAsync(
            adminClient,
            "العلاج الطبيعي",
            "قسم جلسات العلاج الطبيعي",
            "غرفة العلاج الطبيعي");
        Assert.True(physiotherapy.RoomId > 0);

        SpecializationResponse rehabilitation = await PostAndReadAsync<
            CreateSpecializationRequest,
            SpecializationResponse>(
                adminClient,
                $"/api/admin/departments/{physiotherapy.Id}/specializations",
                new CreateSpecializationRequest("التأهيل الحركي"));

        DeviceResponse therapyDevice = await PostAndReadAsync<
            CreateDeviceRequest,
            DeviceResponse>(
                adminClient,
                $"/api/admin/departments/{physiotherapy.Id}/devices",
                new CreateDeviceRequest("جهاز الموجات", "PT-01"));

        ServiceResponse session = await PostAndReadAsync<CreateServiceRequest, ServiceResponse>(
            adminClient,
            "/api/admin/services",
            new CreateServiceRequest(
                physiotherapy.Id,
                rehabilitation.Id,
                "جلسة تأهيل",
                ServiceType.Session,
                45,
                PricingMode.Fixed,
                300m));

        string initialServiceVersion = session.RowVersion;
        session = await PutAndReadAsync<ReplaceDevicesRequest, ServiceResponse>(
            adminClient,
            $"/api/admin/services/{session.Id}/devices",
            new ReplaceDevicesRequest(
                [new DeviceAssignmentRequest(therapyDevice.Id, true)],
                session.RowVersion));
        Assert.Single(session.Devices);
        Assert.NotEqual(initialServiceVersion, session.RowVersion);

        session = await PostAndReadAsync<ChangePriceRequest, ServiceResponse>(
            adminClient,
            $"/api/admin/services/{session.Id}/price",
            new ChangePriceRequest(350m, session.RowVersion));
        Assert.Equal(350m, session.CurrentUnitPrice);

        using HttpResponseMessage priceHistoryResponse = await adminClient.GetAsync(
            $"/api/admin/services/{session.Id}/prices?pageNumber=1&pageSize=20");
        CatalogPageResponse<PriceHistoryResponse>? priceHistory =
            await priceHistoryResponse.Content.ReadFromJsonAsync<
                CatalogPageResponse<PriceHistoryResponse>>();
        Assert.Equal(HttpStatusCode.OK, priceHistoryResponse.StatusCode);
        Assert.Equal(2, priceHistory?.TotalCount);
        Assert.Single(priceHistory!.Items, item => item.EffectiveTo is null);

        using HttpResponseMessage staleUpdate = await PutAsync(
            adminClient,
            $"/api/admin/services/{session.Id}",
            new UpdateServiceRequest(
                "جلسة تأهيل معدلة",
                ServiceType.Session,
                45,
                PricingMode.Fixed,
                initialServiceVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        DepartmentResponse dermatology = await CreateDepartmentAsync(
            adminClient,
            "الجلدية",
            null,
            "غرفة الجلدية");
        DeviceResponse foreignDevice = await PostAndReadAsync<
            CreateDeviceRequest,
            DeviceResponse>(
                adminClient,
                $"/api/admin/departments/{dermatology.Id}/devices",
                new CreateDeviceRequest("جهاز جلدية", "DERM-01"));

        using HttpResponseMessage crossDepartmentAssignment = await PutAsync(
            adminClient,
            $"/api/admin/services/{session.Id}/devices",
            new ReplaceDevicesRequest(
                [new DeviceAssignmentRequest(foreignDevice.Id, true)],
                session.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, crossDepartmentAssignment.StatusCode);

        using HttpResponseMessage archiveDevice = await PostAsync(
            adminClient,
            $"/api/admin/devices/{foreignDevice.Id}/archive",
            new RowVersionRequest(foreignDevice.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, archiveDevice.StatusCode);

        using HttpResponseMessage activeDevicesResponse = await adminClient.GetAsync(
            $"/api/catalog/departments/{dermatology.Id}/devices?pageNumber=1&pageSize=20");
        CatalogPageResponse<DeviceResponse>? activeDevices =
            await activeDevicesResponse.Content.ReadFromJsonAsync<
                CatalogPageResponse<DeviceResponse>>();
        Assert.Equal(HttpStatusCode.OK, activeDevicesResponse.StatusCode);
        Assert.Empty(activeDevices!.Items);

        using HttpResponseMessage blockedArchive = await PostAsync(
            adminClient,
            $"/api/admin/departments/{physiotherapy.Id}/archive",
            new RowVersionRequest(physiotherapy.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, blockedArchive.StatusCode);

        await VerifySecretaryCanReadButCannotWriteAsync(physiotherapy.Id);
        await VerifyDatabaseConstraintsAndAuditAsync(physiotherapy.Id, rehabilitation.Id);
    }

    private async Task VerifySecretaryCanReadButCannotWriteAsync(long departmentId)
    {
        using HttpClient adminClient = _fixture.CreateClient();
        await LoginAsync(
            adminClient,
            "integration.admin",
            IdentitySqlServerFixture.UpdatedAdminPassword);
        await PostAndReadAsync<CreateSecretaryRequest, SecretaryResponse>(
            adminClient,
            "/api/admin/secretaries",
            new CreateSecretaryRequest(
                "سكرتيرة الكتالوج",
                "01055555555",
                null,
                "catalog.secretary",
                "SecretaryPass1"));

        using HttpClient secretaryClient = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(
            secretaryClient,
            "catalog.secretary",
            "SecretaryPass1",
            "SecretaryPass2");

        using HttpResponseMessage readResponse = await secretaryClient.GetAsync(
            "/api/catalog/departments?pageNumber=1&pageSize=50");
        CatalogPageResponse<DepartmentResponse>? departments =
            await readResponse.Content.ReadFromJsonAsync<CatalogPageResponse<DepartmentResponse>>();
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Contains(departments!.Items, item => item.Id == departmentId);

        using HttpResponseMessage forbiddenWrite = await PostAsync(
            secretaryClient,
            "/api/admin/departments",
            new CreateDepartmentRequest("قسم مرفوض", null, "غرفة"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
    }

    private async Task VerifyDatabaseConstraintsAndAuditAsync(
        long departmentId,
        long specializationId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [catalog].[Rooms] ([DepartmentId], [Name], [IsActive], [IsArchived]) VALUES ({departmentId}, {"غرفة مكررة"}, {true}, {false})"));

        long anotherDepartmentId = await dbContext.Departments
            .Where(item => item.Id != departmentId)
            .Select(item => item.Id)
            .FirstAsync();
        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [catalog].[Services] ([DepartmentId], [SpecializationId], [Name], [ServiceType], [DurationMinutes], [PricingMode], [CurrentUnitPrice], [IsActive], [IsArchived]) VALUES ({anotherDepartmentId}, {specializationId}, {"خدمة بعلاقة خاطئة"}, {3}, {30}, {1}, {100m}, {true}, {false})"));

        int auditCount = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.StartsWith("catalog."));
        Assert.True(auditCount >= 7);
    }

    private static Task<DepartmentResponse> CreateDepartmentAsync(
        HttpClient client,
        string name,
        string? description,
        string roomName) =>
        PostAndReadAsync<CreateDepartmentRequest, DepartmentResponse>(
            client,
            "/api/admin/departments",
            new CreateDepartmentRequest(name, description, roomName));

    private static async Task LoginAndChangePasswordAsync(
        HttpClient client,
        string userName,
        string currentPassword,
        string newPassword)
    {
        await LoginAsync(client, userName, currentPassword);
        using HttpResponseMessage response = await PostAsync(
            client,
            "/api/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(
        HttpClient client,
        string userName,
        string password)
    {
        using HttpResponseMessage response = await PostAsync(
            client,
            "/api/auth/login",
            new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, requestUri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from {requestUri}, received {(int)response.StatusCode}: {body}");
        TResponse? payload = await response.Content.ReadFromJsonAsync<TResponse>();
        return payload ?? throw new InvalidOperationException("The API response was empty.");
    }

    private static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        using HttpResponseMessage response = await PutAsync(client, requestUri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from {requestUri}, received {(int)response.StatusCode}: {body}");
        TResponse? payload = await response.Content.ReadFromJsonAsync<TResponse>();
        return payload ?? throw new InvalidOperationException("The API response was empty.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        string csrfToken = await GetCsrfTokenAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrfToken);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> PutAsync<TRequest>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        string csrfToken = await GetCsrfTokenAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrfToken);
        return await client.SendAsync(message);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        CsrfResponse? payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        return payload?.Token ?? throw new InvalidOperationException(
            "The API did not return an antiforgery token.");
    }

    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CreateDepartmentRequest(string Name, string? Description, string RoomName);
    private sealed record RowVersionRequest(string RowVersion);
    private sealed record CreateSpecializationRequest(string Name);
    private sealed record CreateDeviceRequest(string Name, string? Identifier);
    private sealed record CreateServiceRequest(
        long DepartmentId,
        long SpecializationId,
        string Name,
        ServiceType ServiceType,
        int DurationMinutes,
        PricingMode PricingMode,
        decimal UnitPrice);
    private sealed record UpdateServiceRequest(
        string Name,
        ServiceType ServiceType,
        int DurationMinutes,
        PricingMode PricingMode,
        string RowVersion);
    private sealed record ChangePriceRequest(decimal UnitPrice, string RowVersion);
    private sealed record DeviceAssignmentRequest(long DeviceId, bool IsRequired);
    private sealed record ReplaceDevicesRequest(
        IReadOnlyCollection<DeviceAssignmentRequest> Devices,
        string RowVersion);
    private sealed record CreateSecretaryRequest(
        string FullName,
        string PhoneNumber,
        string? Email,
        string UserName,
        string TemporaryPassword);
    private sealed record SecretaryResponse(long Id);
    private sealed record DepartmentResponse(
        long Id,
        string Name,
        string? Description,
        DepartmentStatus Status,
        bool IsArchived,
        long RoomId,
        string RoomName,
        string RowVersion);
    private sealed record SpecializationResponse(long Id, string RowVersion);
    private sealed record DeviceResponse(long Id, string RowVersion);
    private sealed record ServiceDeviceResponse(long DeviceId, bool IsRequired);
    private sealed record ServiceResponse(
        long Id,
        decimal CurrentUnitPrice,
        IReadOnlyCollection<ServiceDeviceResponse> Devices,
        string RowVersion);
    private sealed record PriceHistoryResponse(
        long Id,
        decimal UnitPrice,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo);
    private sealed record CatalogPageResponse<T>(
        IReadOnlyCollection<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
