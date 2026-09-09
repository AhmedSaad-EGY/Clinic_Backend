using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Catalog;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Scheduling;

public sealed class SchedulingFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;
    public SchedulingFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AdminConfiguresDoctorHoursAndClosureWhileSecretaryHasReadOnlyAccess()
    {
        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin, "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);

        DepartmentResponse department = await PostReadAsync<CreateDepartmentRequest, DepartmentResponse>(
            admin, "/api/admin/departments", new("العلاج الطبيعي", null, "غرفة العلاج"));
        SpecializationResponse specialization = await PostReadAsync<CreateSpecializationRequest, SpecializationResponse>(
            admin, $"/api/admin/departments/{department.Id}/specializations", new("التأهيل"));
        ServiceResponse service = await PostReadAsync<CreateServiceRequest, ServiceResponse>(
            admin, "/api/admin/services", new(department.Id, specialization.Id, "جلسة",
                ServiceType.Session, 30, PricingMode.Fixed, 200m));
        DoctorResponse doctor = await PostReadAsync<CreateDoctorRequest, DoctorResponse>(
            admin, "/api/admin/doctors", new(department.Id, "د. أحمد", null));
        doctor = await PutReadAsync<ReplaceServicesRequest, DoctorResponse>(
            admin, $"/api/admin/doctors/{doctor.Id}/services", new([service.Id], doctor.RowVersion));
        Assert.Single(doctor.Services);

        DepartmentResponse otherDepartment = await PostReadAsync<CreateDepartmentRequest, DepartmentResponse>(
            admin, "/api/admin/departments", new("الجلدية", null, "غرفة الجلدية"));
        SpecializationResponse otherSpecialization = await PostReadAsync<CreateSpecializationRequest, SpecializationResponse>(
            admin, $"/api/admin/departments/{otherDepartment.Id}/specializations", new("جلدية"));
        ServiceResponse foreignService = await PostReadAsync<CreateServiceRequest, ServiceResponse>(
            admin, "/api/admin/services", new(otherDepartment.Id, otherSpecialization.Id,
                "كشف جلدية", ServiceType.Consultation, 20, PricingMode.Fixed, 250m));
        await using (AsyncServiceScope scope = _fixture.Services.CreateAsyncScope())
        {
            ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [scheduling].[DoctorServices] ([DoctorId], [ServiceId], [DepartmentId], [IsActive]) VALUES ({doctor.Id}, {foreignService.Id}, {otherDepartment.Id}, {true})"));
        }

        ScheduleResponse schedule = await PostReadAsync<CreateScheduleRequest, ScheduleResponse>(
            admin, $"/api/admin/scheduling/doctors/{doctor.Id}/schedules",
            new(ClinicDayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0),
                new DateOnly(2026, 9, 7), null));
        Assert.True(schedule.IsActive);
        SchedulingPageResponse<ScheduleResponse>? schedules =
            await admin.GetFromJsonAsync<SchedulingPageResponse<ScheduleResponse>>(
                $"/api/admin/scheduling/doctors/{doctor.Id}/schedules");
        Assert.Contains(schedules?.Items ?? [], item => item.Id == schedule.Id);

        using HttpResponseMessage overlap = await PostAsync(admin,
            $"/api/admin/scheduling/doctors/{doctor.Id}/schedules",
            new CreateScheduleRequest(ClinicDayOfWeek.Monday, new TimeOnly(13, 0),
                new TimeOnly(15, 0), new DateOnly(2026, 9, 7), null));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);

        using HttpResponseMessage staleDoctorUpdate = await PutAsync(admin,
            $"/api/admin/doctors/{doctor.Id}",
            new UpdateDoctorRequest("د. أحمد", null, true, department.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleDoctorUpdate.StatusCode);

        DateTimeOffset closureStart = new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);
        _ = await PostReadAsync<CreateClosureRequest, ClosureResponse>(admin,
            $"/api/admin/scheduling/departments/{department.Id}/closures",
            new(closureStart, closureStart.AddHours(4), "صيانة"));
        using HttpResponseMessage overlappingClosure = await PostAsync(admin,
            $"/api/admin/scheduling/departments/{department.Id}/closures",
            new CreateClosureRequest(closureStart.AddHours(1), closureStart.AddHours(5), "إغلاق آخر"));
        Assert.Equal(HttpStatusCode.Conflict, overlappingClosure.StatusCode);
        AvailabilityResponse availability = await admin.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/scheduling/departments/{department.Id}/availability?at={Uri.EscapeDataString(closureStart.AddHours(1).ToString("O"))}")
            ?? throw new InvalidOperationException("Missing availability response.");
        Assert.Equal(DepartmentStatus.Stopped, availability.Status);

        await VerifyConcurrentMutationsPreserveArchiveInvariantsAsync(
            admin,
            department.Id,
            specialization.Id);

        _ = await PostReadAsync<CreateSecretaryRequest, SecretaryResponse>(admin,
            "/api/admin/secretaries", new("سكرتيرة", "01012345678", null,
                "scheduling.secretary", "SecretaryPass1"));
        using HttpClient secretary = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(secretary, "scheduling.secretary", "SecretaryPass1", "SecretaryPass2");
        using HttpResponseMessage allowed = await secretary.GetAsync(
            $"/api/scheduling/services/{service.Id}/doctors");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        SchedulingPageResponse<DoctorResponse>? doctorPage =
            await secretary.GetFromJsonAsync<SchedulingPageResponse<DoctorResponse>>(
                $"/api/scheduling/doctors?departmentId={department.Id}&pageNumber=1&pageSize=1");
        Assert.Single(doctorPage?.Items ?? []);
        Assert.True(doctorPage?.TotalCount >= 2);
        using HttpResponseMessage invalidPage = await secretary.GetAsync(
            "/api/scheduling/doctors?pageNumber=1&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using HttpResponseMessage forbidden = await PostAsync(secretary,
            $"/api/admin/scheduling/doctors/{doctor.Id}/schedules",
            new CreateScheduleRequest(ClinicDayOfWeek.Tuesday, new TimeOnly(10, 0),
                new TimeOnly(11, 0), new DateOnly(2026, 9, 8), null));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private async Task VerifyConcurrentMutationsPreserveArchiveInvariantsAsync(
        HttpClient firstAdmin,
        long departmentId,
        long specializationId)
    {
        ServiceResponse raceService = await PostReadAsync<CreateServiceRequest, ServiceResponse>(
            firstAdmin,
            "/api/admin/services",
            new CreateServiceRequest(
                departmentId,
                specializationId,
                "خدمة اختبار التزامن",
                ServiceType.Session,
                15,
                PricingMode.Fixed,
                100m));
        DoctorResponse serviceRaceDoctor = await PostReadAsync<CreateDoctorRequest, DoctorResponse>(
            firstAdmin,
            "/api/admin/doctors",
            new CreateDoctorRequest(departmentId, "طبيب اختبار ربط", null));

        using HttpClient secondAdmin = _fixture.CreateClient();
        await LoginAsync(
            secondAdmin,
            "integration.admin",
            IdentitySqlServerFixture.UpdatedAdminPassword);
        Task<HttpResponseMessage> replaceTask = PutAsync(
            firstAdmin,
            $"/api/admin/doctors/{serviceRaceDoctor.Id}/services",
            new ReplaceServicesRequest([raceService.Id], serviceRaceDoctor.RowVersion));
        Task<HttpResponseMessage> archiveServiceTask = PostAsync(
            secondAdmin,
            $"/api/admin/services/{raceService.Id}/archive",
            new RowVersionRequest(raceService.RowVersion));
        await Task.WhenAll(replaceTask, archiveServiceTask);
        using HttpResponseMessage replaceResponse = await replaceTask;
        using HttpResponseMessage archiveServiceResponse = await archiveServiceTask;
        Assert.Contains(
            replaceResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        Assert.Contains(
            archiveServiceResponse.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });

        DoctorResponse doctorRace = await PostReadAsync<CreateDoctorRequest, DoctorResponse>(
            firstAdmin,
            "/api/admin/doctors",
            new CreateDoctorRequest(departmentId, "طبيب اختبار الأرشفة", null));
        Task<HttpResponseMessage> createScheduleTask = PostAsync(
            firstAdmin,
            $"/api/admin/scheduling/doctors/{doctorRace.Id}/schedules",
            new CreateScheduleRequest(
                ClinicDayOfWeek.Wednesday,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                new DateOnly(2026, 9, 9),
                null));
        Task<HttpResponseMessage> archiveDoctorTask = PostAsync(
            secondAdmin,
            $"/api/admin/doctors/{doctorRace.Id}/archive",
            new RowVersionRequest(doctorRace.RowVersion));
        await Task.WhenAll(createScheduleTask, archiveDoctorTask);
        using HttpResponseMessage createScheduleResponse = await createScheduleTask;
        using HttpResponseMessage archiveDoctorResponse = await archiveDoctorTask;
        Assert.Contains(
            createScheduleResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.NoContent, archiveDoctorResponse.StatusCode);

        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        bool serviceArchived = await dbContext.Services.AsNoTracking()
            .Where(item => item.Id == raceService.Id)
            .Select(item => item.IsArchived)
            .SingleAsync();
        bool activeAssignment = await dbContext.DoctorServices.AsNoTracking().AnyAsync(
            item => item.DoctorId == serviceRaceDoctor.Id &&
                    item.ServiceId == raceService.Id && item.IsActive);
        Assert.False(serviceArchived && activeAssignment);

        bool doctorArchived = await dbContext.Doctors.AsNoTracking()
            .Where(item => item.Id == doctorRace.Id)
            .Select(item => item.IsArchived)
            .SingleAsync();
        bool activeSchedule = await dbContext.DoctorSchedules.AsNoTracking().AnyAsync(
            item => item.DoctorId == doctorRace.Id && item.IsActive);
        Assert.True(doctorArchived);
        Assert.False(activeSchedule);
    }

    private static async Task LoginAndChangePasswordAsync(HttpClient client, string userName, string password, string newPassword)
    {
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/auth/login", new LoginRequest(userName, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/auth/change-password", new ChangePasswordRequest(password, newPassword))).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        using HttpResponseMessage response = await PostAsync(
            client,
            "/api/auth/login",
            new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<TResponse> PostReadAsync<TRequest, TResponse>(HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{uri}: {(int)response.StatusCode} {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ?? throw new InvalidOperationException("Missing response.");
    }

    private static async Task<TResponse> PutReadAsync<TRequest, TResponse>(HttpClient client, string uri, TRequest request)
    {
        string csrf = await GetCsrfAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Put, uri) { Content = JsonContent.Create(request) };
        message.Headers.Add("X-XSRF-TOKEN", csrf);
        using HttpResponseMessage response = await client.SendAsync(message);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{uri}: {(int)response.StatusCode} {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ?? throw new InvalidOperationException("Missing response.");
    }

    private static async Task<HttpResponseMessage> PutAsync<TRequest>(HttpClient client, string uri, TRequest request)
    {
        string csrf = await GetCsrfAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Put, uri) { Content = JsonContent.Create(request) };
        message.Headers.Add("X-XSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(HttpClient client, string uri, TRequest request)
    {
        string csrf = await GetCsrfAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Post, uri) { Content = JsonContent.Create(request) };
        message.Headers.Add("X-XSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfResponse>())?.Token
        ?? throw new InvalidOperationException("Missing CSRF token.");

    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CreateDepartmentRequest(string Name, string? Description, string RoomName);
    private sealed record DepartmentResponse(long Id, string RowVersion);
    private sealed record CreateSpecializationRequest(string Name);
    private sealed record SpecializationResponse(long Id);
    private sealed record CreateServiceRequest(long DepartmentId, long SpecializationId, string Name, ServiceType ServiceType, int DurationMinutes, PricingMode PricingMode, decimal UnitPrice);
    private sealed record ServiceResponse(long Id, string RowVersion);
    private sealed record CreateDoctorRequest(long DepartmentId, string Name, string? Phone);
    private sealed record ReplaceServicesRequest(IReadOnlyCollection<long> ServiceIds, string RowVersion);
    private sealed record RowVersionRequest(string RowVersion);
    private sealed record UpdateDoctorRequest(string Name, string? Phone, bool IsActive, string RowVersion);
    private sealed record DoctorServiceResponse(long ServiceId);
    private sealed record DoctorResponse(long Id, IReadOnlyCollection<DoctorServiceResponse> Services, string RowVersion);
    private sealed record CreateScheduleRequest(ClinicDayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    private sealed record ScheduleResponse(long Id, bool IsActive);
    private sealed record CreateClosureRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason);
    private sealed record ClosureResponse(long Id);
    private sealed record AvailabilityResponse(DepartmentStatus Status);
    private sealed record SchedulingPageResponse<T>(IReadOnlyCollection<T> Items, int TotalCount);
    private sealed record CreateSecretaryRequest(string FullName, string PhoneNumber, string? Email, string UserName, string TemporaryPassword);
    private sealed record SecretaryResponse(long Id);
}
