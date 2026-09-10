using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Catalog;
using Clinic.Domain.Discounts;
using Clinic.Domain.Packages;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Packages;

public sealed class PatientPackageFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0,
        TimeSpan.Zero);
    private readonly IdentitySqlServerFixture _fixture;
    public PatientPackageFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RegistrationSnapshotsSessionsIsIdempotentAndHonorsAvailability()
    {
        _fixture.Clock.SetUtcNow(Now);
        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin, "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);
        Setup setup = await CreateSetupAsync(admin, "مدفوعة", 900m, 3);
        DiscountResponse discount = await ReadAsync<DiscountResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/discounts", new DiscountRequest("خصم شراء باقة",
                DiscountType.FixedAmount, 200m, DiscountAppliesTo.Packages,
                DiscountScopeMode.Selected, Now.AddHours(-1), Now.AddHours(1),
                new([], [], [setup.PackageId])), HttpStatusCode.Created));

        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{setup.PatientId}/packages",
            new RegisterRequest(setup.PackageId, setup.PackageRowVersion),
            HttpStatusCode.BadRequest);

        Guid key = Guid.NewGuid();
        PatientPackageResponse registered = await ReadAsync<PatientPackageResponse>(
            await SendAsync(admin, HttpMethod.Post,
                $"/api/patients/{setup.PatientId}/packages",
                new RegisterRequest(setup.PackageId, setup.PackageRowVersion),
                HttpStatusCode.Created, key));
        Assert.Equal(PatientPackagePaymentStatus.Unpaid, registered.PaymentStatus);
        Assert.Equal(discount.Id, registered.DiscountId);
        Assert.Equal(200m, registered.DiscountAmount);
        Assert.Equal(700m, registered.NetPrice);
        Assert.False(registered.IsUsable);
        Assert.Equal(3, registered.AvailableSessions);
        Assert.Null(registered.ActivationDeadlineAt);

        HttpResponseMessage replayResponse = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{setup.PatientId}/packages",
            new RegisterRequest(setup.PackageId, setup.PackageRowVersion),
            HttpStatusCode.OK, key);
        Assert.Equal("true", replayResponse.Headers.GetValues("Idempotency-Replayed").Single());
        PatientPackageResponse replay = await ReadAsync<PatientPackageResponse>(replayResponse);
        Assert.Equal(registered.Id, replay.Id);

        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{setup.PatientId}/packages",
            new RegisterRequest(setup.PackageId, Convert.ToBase64String(new byte[8])),
            HttpStatusCode.Conflict, key);
        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{setup.PatientId}/packages",
            new RegisterRequest(long.MaxValue, setup.PackageRowVersion),
            HttpStatusCode.Conflict, key);
        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{setup.PatientId}/packages",
            new RegisterRequest(setup.PackageId, Convert.ToBase64String(new byte[8])),
            HttpStatusCode.Conflict, Guid.NewGuid());

        IReadOnlyCollection<SessionResponse> sessions = await ReadAsync<
            IReadOnlyCollection<SessionResponse>>(await admin.GetAsync(
                $"/api/patient-packages/{registered.Id}/sessions"));
        Assert.Equal([1, 2, 3], sessions.Select(item => item.SequenceNumber));
        Assert.All(sessions, item => Assert.Equal(PackageSessionStatus.Available, item.Status));
        await VerifyPurchasedDefinitionConstraintAsync(admin, setup, registered.Id);

        await VerifyFreePackageExtensionAndClosureAsync(admin, setup.PatientId);
        await VerifySecretaryCanRegisterButCannotExtendAsync(admin);
        await VerifyConcurrentReplayCreatesOnePurchaseAsync(admin);
    }

    private static async Task VerifyFreePackageExtensionAndClosureAsync(HttpClient admin,
        long patientId)
    {
        Setup free = await CreateSetupAsync(admin, "مجانية", 0m, 2, patientId);
        PatientPackageResponse registered = await ReadAsync<PatientPackageResponse>(
            await SendAsync(admin, HttpMethod.Post, $"/api/patients/{patientId}/packages",
                new RegisterRequest(free.PackageId, free.PackageRowVersion),
                HttpStatusCode.Created, Guid.NewGuid()));
        Assert.Equal(PatientPackagePaymentStatus.NotRequired, registered.PaymentStatus);
        Assert.True(registered.IsUsable);
        Assert.Equal(Now.AddDays(14), registered.ActivationDeadlineAt);

        PatientPackageResponse extended = await ReadAsync<PatientPackageResponse>(
            await SendAsync(admin, HttpMethod.Post,
                $"/api/admin/patient-packages/{registered.Id}/extend",
                new ExtendRequest(PatientPackageExtensionType.ActivationDeadline,
                    Now.AddDays(21), "توقف استثنائي", registered.RowVersion),
                HttpStatusCode.OK));
        Assert.Equal(Now.AddDays(21), extended.ActivationDeadlineAt);

        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/admin/scheduling/departments/{free.DepartmentId}/closures",
            new ClosureRequest(Now.AddMinutes(-1), Now.AddHours(1), "توقف اختبار"),
            HttpStatusCode.OK);
        PatientPackageResponse stopped = await ReadAsync<PatientPackageResponse>(
            await admin.GetAsync($"/api/patient-packages/{registered.Id}"));
        Assert.False(stopped.IsUsable);
        Assert.Equal("القسم متوقف مؤقتًا.", stopped.UnavailabilityReason);
        _ = await SendAsync(admin, HttpMethod.Post,
            $"/api/patients/{patientId}/packages",
            new RegisterRequest(free.PackageId, free.PackageRowVersion),
            HttpStatusCode.Conflict, Guid.NewGuid());
    }

    private async Task VerifySecretaryCanRegisterButCannotExtendAsync(HttpClient admin)
    {
        Setup setup = await CreateSetupAsync(admin, "للسكرتيرة", 0m, 1);
        _ = await SendAsync(admin, HttpMethod.Post, "/api/admin/secretaries",
            new SecretaryRequest("سكرتيرة باقات", "01055555555", null,
                "patient.package.secretary", "SecretaryPass1"), HttpStatusCode.OK);
        using HttpClient secretary = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(secretary, "patient.package.secretary",
            "SecretaryPass1", "SecretaryPass2");

        PatientPackageResponse registered = await ReadAsync<PatientPackageResponse>(
            await SendAsync(secretary, HttpMethod.Post,
                $"/api/patients/{setup.PatientId}/packages",
                new RegisterRequest(setup.PackageId, setup.PackageRowVersion),
                HttpStatusCode.Created, Guid.NewGuid()));
        _ = await SendAsync(secretary, HttpMethod.Post,
            $"/api/admin/patient-packages/{registered.Id}/extend",
            new ExtendRequest(PatientPackageExtensionType.ActivationDeadline,
                Now.AddDays(20), "غير مسموح", registered.RowVersion),
            HttpStatusCode.Forbidden);
    }

    private static async Task VerifyConcurrentReplayCreatesOnePurchaseAsync(HttpClient admin)
    {
        Setup setup = await CreateSetupAsync(admin, "متزامنة", 400m, 2);
        Guid key = Guid.NewGuid();
        string token = (await ReadAsync<CsrfResponse>(await admin.GetAsync("/api/auth/csrf"))).Token;
        HttpRequestMessage BuildRequest()
        {
            HttpRequestMessage request = new(HttpMethod.Post,
                $"/api/patients/{setup.PatientId}/packages")
            {
                Content = JsonContent.Create(new RegisterRequest(setup.PackageId,
                    setup.PackageRowVersion))
            };
            request.Headers.Add("X-XSRF-TOKEN", token);
            request.Headers.Add("Idempotency-Key", key.ToString());
            return request;
        }

        using HttpRequestMessage firstRequest = BuildRequest();
        using HttpRequestMessage secondRequest = BuildRequest();
        HttpResponseMessage[] responses = await Task.WhenAll(admin.SendAsync(firstRequest),
            admin.SendAsync(secondRequest));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created],
            responses.Select(item => item.StatusCode).Order().ToArray());
        PatientPackageResponse[] purchases = await Task.WhenAll(responses.Select(
            ReadAsync<PatientPackageResponse>));
        Assert.Single(purchases.Select(item => item.Id).Distinct());
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    private async Task VerifyPurchasedDefinitionConstraintAsync(HttpClient admin, Setup setup,
        long patientPackageId)
    {
        PackageResponse anotherPackage = await ReadAsync<PackageResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/packages", new PackageRequest(setup.DepartmentId,
                "باقة بديلة لاختبار القيد", 800m, 14, 90,
                [new PackageLineRequest(setup.ServiceId, 3, 300m)]),
            HttpStatusCode.Created));

        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        long anotherSourceLineId = await dbContext.PackageServices.AsNoTracking()
            .Where(item => item.PackageId == anotherPackage.Id &&
                           item.ServiceId == setup.ServiceId)
            .Select(item => item.Id)
            .SingleAsync();

        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [packages].[PatientPackageServices]
                SET [SourcePackageServiceId] = {anotherSourceLineId},
                    [SourcePackageId] = {anotherPackage.Id}
                WHERE [PatientPackageId] = {patientPackageId}
                """));
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [packages].[PatientPackages]
                SET [DiscountAmountSnapshot] = [BasePriceSnapshot] + {1m},
                    [NetPriceSnapshot] = {-1m}
                WHERE [Id] = {patientPackageId}
                """));
    }

    private static async Task<Setup> CreateSetupAsync(HttpClient admin, string suffix,
        decimal price, int sessions, long? existingPatientId = null)
    {
        DepartmentResponse department = await ReadAsync<DepartmentResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/departments",
            new DepartmentRequest($"قسم {suffix}", null, $"غرفة {suffix}"), HttpStatusCode.OK));
        IdResponse specialization = await ReadAsync<IdResponse>(await SendAsync(admin,
            HttpMethod.Post, $"/api/admin/departments/{department.Id}/specializations",
            new NameRequest($"تخصص {suffix}"), HttpStatusCode.OK));
        ServiceResponse service = await ReadAsync<ServiceResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/services", new ServiceRequest(department.Id,
                specialization.Id, $"خدمة {suffix}", ServiceType.Session, 30,
                PricingMode.Fixed, 300m), HttpStatusCode.OK));
        PackageResponse package = await ReadAsync<PackageResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/admin/packages", new PackageRequest(department.Id,
                $"باقة {suffix}", price, 14, 90,
                [new PackageLineRequest(service.Id, sessions, 300m)]),
            HttpStatusCode.Created));
        long patientId = existingPatientId ?? (await ReadAsync<IdResponse>(await SendAsync(admin,
            HttpMethod.Post, "/api/patients", new PatientRequest($"مريض {suffix}",
                PhoneFor(suffix), null, null, 30, PatientGender.Male, null, null, null, null,
                null), HttpStatusCode.OK))).Id;
        return new Setup(patientId, department.Id, service.Id, package.Id,
            package.RowVersion);
    }

    private static string PhoneFor(string suffix) => suffix switch
    {
        "مدفوعة" => "01060000001",
        "للسكرتيرة" => "01060000002",
        "متزامنة" => "01060000003",
        _ => "01060000004"
    };

    private static async Task LoginAndChangePasswordAsync(HttpClient client, string userName,
        string currentPassword, string newPassword)
    {
        _ = await SendAsync(client, HttpMethod.Post, "/api/auth/login",
            new LoginRequest(userName, currentPassword), HttpStatusCode.OK);
        _ = await SendAsync(client, HttpMethod.Post, "/api/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword), HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendAsync<T>(HttpClient client,
        HttpMethod method, string uri, T body, HttpStatusCode expected, Guid? idempotencyKey = null)
    {
        string token = (await ReadAsync<CsrfResponse>(await client.GetAsync("/api/auth/csrf"))).Token;
        HttpRequestMessage request = new(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        if (idempotencyKey.HasValue)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey.Value.ToString());
        }

        HttpResponseMessage response = await client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected} from {uri}, got {(int)response.StatusCode}: {content}");
        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>() ??
        throw new InvalidOperationException("The API response was empty.");

    private sealed record Setup(long PatientId, long DepartmentId, long ServiceId,
        long PackageId, string PackageRowVersion);
    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record SecretaryRequest(string FullName, string PhoneNumber, string? Email,
        string UserName, string TemporaryPassword);
    private sealed record DepartmentRequest(string Name, string? Description, string RoomName);
    private sealed record NameRequest(string Name);
    private sealed record ServiceRequest(long DepartmentId, long SpecializationId, string Name,
        ServiceType ServiceType, int DurationMinutes, PricingMode PricingMode, decimal UnitPrice);
    private sealed record PackageRequest(long DepartmentId, string Name, decimal BasePrice,
        int ActivationGraceDays, int UsageDurationDays,
        IReadOnlyCollection<PackageLineRequest> Services);
    private sealed record PackageLineRequest(long ServiceId, int SessionsIncluded,
        decimal UnitPriceAtDefinition);
    private sealed record PatientRequest(string FullName, string PrimaryPhoneNumber,
        string? SecondaryPhoneNumber, DateOnly? BirthDate, int? Age, PatientGender Gender,
        string? Area, string? Address, string? Email, string? GuardianName,
        string? GuardianPhoneNumber);
    private sealed record RegisterRequest(long PackageId, string PackageRowVersion);
    private sealed record ExtendRequest(PatientPackageExtensionType ExtensionType,
        DateTimeOffset NewDeadline, string Reason, string RowVersion);
    private sealed record ClosureRequest(DateTimeOffset StartAt, DateTimeOffset EndAt,
        string Reason);
    private sealed record IdResponse(long Id);
    private sealed record DepartmentResponse(long Id);
    private sealed record ServiceResponse(long Id);
    private sealed record PackageResponse(long Id, string RowVersion);
    private sealed record DiscountTargetsRequest(IReadOnlyCollection<long> DepartmentIds,
        IReadOnlyCollection<long> ServiceIds, IReadOnlyCollection<long> PackageIds);
    private sealed record DiscountRequest(string Name, DiscountType Type, decimal Value,
        DiscountAppliesTo AppliesTo, DiscountScopeMode ScopeMode,
        DateTimeOffset StartAt, DateTimeOffset EndAt, DiscountTargetsRequest Targets);
    private sealed record DiscountResponse(long Id);
    private sealed record PatientPackageResponse(long Id,
        PatientPackagePaymentStatus PaymentStatus, bool IsUsable, string? UnavailabilityReason,
        int AvailableSessions, DateTimeOffset? ActivationDeadlineAt, string RowVersion,
        long? DiscountId, decimal DiscountAmount, decimal NetPrice);
    private sealed record SessionResponse(int SequenceNumber, PackageSessionStatus Status);
}
