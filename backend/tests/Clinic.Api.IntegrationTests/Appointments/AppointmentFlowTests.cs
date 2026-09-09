using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Appointments;
using Clinic.Domain.Catalog;
using Clinic.Domain.Patients;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Appointments;

public sealed class AppointmentFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public AppointmentFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BookingFlowPreventsConflictsAndPreservesSnapshots()
    {
        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin);
        SeedData data = await SeedAsync();

        CreateAppointmentRequest request = new(data.PatientId, data.DepartmentId,
            data.StartAt, [new(data.ServiceId, data.DoctorId, 1, [])]);
        AppointmentResponse created = await PostAndReadAsync<CreateAppointmentRequest,
            AppointmentResponse>(admin, "/api/appointments", request);
        Assert.Equal(AppointmentStatus.Booked, created.Status);
        Assert.Equal(300m, created.NetAmount);
        Assert.Equal(data.StartAt.AddMinutes(30), created.EndAt);

        _ = await PostAndReadAsync<CreateAppointmentRequest, AppointmentResponse>(admin,
            "/api/appointments", request with { StartAt = data.StartAt.AddMinutes(-30) });
        AppointmentAvailabilityResponse availability = await PostAndReadAsync<
            CheckAppointmentAvailabilityRequest, AppointmentAvailabilityResponse>(admin,
                "/api/appointments/availability",
                new(data.PatientId, data.DepartmentId, data.StartAt.AddMinutes(-15),
                    request.Services, created.Id));
        Assert.False(availability.IsAvailable);
        Assert.Equal(data.StartAt, availability.Alternatives.First().StartAt);

        AppointmentResponse transferred = await PostAndReadAsync<TransferDoctorRequest,
            AppointmentResponse>(admin,
                $"/api/admin/appointments/{created.Id}/services/{created.Services.Single().Id}/transfer-doctor",
                new(data.AlternativeDoctorId, "تغيير الطبيب", created.RowVersion));
        Assert.NotEqual(created.RowVersion, transferred.RowVersion);
        using HttpResponseMessage staleTransfer = await PostAsync(admin,
            $"/api/admin/appointments/{created.Id}/services/{created.Services.Single().Id}/transfer-doctor",
            new TransferDoctorRequest(data.DoctorId, "نسخة قديمة", created.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleTransfer.StatusCode);

        using HttpResponseMessage conflict = await PostAsync(admin, "/api/appointments", request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        string conflictBody = await conflict.Content.ReadAsStringAsync();
        Assert.Contains("alternatives", conflictBody, StringComparison.Ordinal);

        CreateAppointmentRequest concurrentRequest = request with
        {
            StartAt = data.StartAt.AddHours(2)
        };
        HttpResponseMessage[] concurrent = await Task.WhenAll(
            PostAsync(admin, "/api/appointments", concurrentRequest),
            PostAsync(admin, "/api/appointments", concurrentRequest));
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (HttpResponseMessage response in concurrent) response.Dispose();

        AppointmentResponse updated = await PutAndReadAsync<UpdateAppointmentRequest,
            AppointmentResponse>(admin, $"/api/appointments/{created.Id}",
                new(data.PatientId, data.DepartmentId, data.StartAt,
                    [new(data.AlternativeServiceId, data.AlternativeDoctorId, 1, [])],
                    transferred.RowVersion));
        Assert.Single(updated.Services);
        Assert.Equal(data.AlternativeServiceId, updated.Services.Single().ServiceId);

        AppointmentPageResponse? oldServiceSearch = await admin.GetFromJsonAsync<
            AppointmentPageResponse>($"/api/admin/appointments?serviceId={data.ServiceId}");
        Assert.DoesNotContain(oldServiceSearch?.Items ?? [], item => item.Id == created.Id);

        await SetPatientAgeObservationAsync(data.PatientId, 30,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));
        AppointmentPageResponse? ageSearch = await admin.GetFromJsonAsync<AppointmentPageResponse>(
            "/api/admin/appointments?minimumAge=32&maximumAge=32");
        Assert.Contains(ageSearch?.Items ?? [], item => item.Id == created.Id);

        using HttpResponseMessage oldDoctorException = await PostAsync(admin,
            $"/api/admin/scheduling/doctors/{data.DoctorId}/exceptions",
            new CreateDoctorExceptionRequest(data.ClinicDate, new TimeOnly(10, 0),
                new TimeOnly(10, 30), DoctorExceptionType.Unavailable, "غياب", false));
        Assert.Equal(HttpStatusCode.OK, oldDoctorException.StatusCode);

        await Assert.ThrowsAsync<SqlException>(() => AddMismatchedDeviceAsync(
            updated.Services.Single().Id, data, updated.StartAt, updated.EndAt));

        await SetAppointmentStatusAsync(created.Id, AppointmentStatus.Confirmed);

        IReadOnlyCollection<AppointmentResponse>? calendar = await admin.GetFromJsonAsync<
            IReadOnlyCollection<AppointmentResponse>>(
                $"/api/appointments/calendar?date={data.ClinicDate:yyyy-MM-dd}");
        Assert.Contains(calendar ?? [], item => item.Id == created.Id);

        CreateClosureRequest closureRequest = new(data.StartAt,
            data.StartAt.AddHours(2), "صيانة", false);
        using HttpResponseMessage closureWarning = await PostAsync(admin,
            $"/api/admin/scheduling/departments/{data.DepartmentId}/closures",
            closureRequest);
        Assert.Equal(HttpStatusCode.Conflict, closureWarning.StatusCode);
        ClosureResponse closure = await PostAndReadAsync<CreateClosureRequest,
            ClosureResponse>(admin,
                $"/api/admin/scheduling/departments/{data.DepartmentId}/closures",
                closureRequest with { ConfirmAffectedAppointments = true });
        AppointmentResponse suspended = await admin.GetFromJsonAsync<AppointmentResponse>(
            $"/api/appointments/{created.Id}") ?? throw new InvalidOperationException();
        Assert.Equal(AppointmentStatus.Suspended, suspended.Status);

        using HttpResponseMessage cancelClosure = await PostAsync(admin,
            $"/api/admin/scheduling/closures/{closure.Id}/cancel",
            new SchedulingVersionRequest(closure.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, cancelClosure.StatusCode);
        using HttpResponseMessage revalidated = await PostAsync(admin,
            "/api/admin/appointments/suspended/revalidate", new { });
        Assert.Equal(HttpStatusCode.OK, revalidated.StatusCode);
        AppointmentResponse activeAgain = await admin.GetFromJsonAsync<AppointmentResponse>(
            $"/api/appointments/{created.Id}") ?? throw new InvalidOperationException();
        Assert.Equal(AppointmentStatus.Confirmed, activeAgain.Status);

        AppointmentResponse cancellable = await PostAndReadAsync<CreateAppointmentRequest,
            AppointmentResponse>(admin, "/api/appointments", request with
            {
                StartAt = data.StartAt.AddHours(3)
            });
        using HttpResponseMessage cancelled = await PostAsync(admin,
            $"/api/appointments/{cancellable.Id}/cancel",
            new ChangeStateRequest(cancellable.RowVersion, null));
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        AppointmentResponse cancelledDetails = await admin.GetFromJsonAsync<AppointmentResponse>(
            $"/api/appointments/{cancellable.Id}") ?? throw new InvalidOperationException();
        Assert.Single(cancelledDetails.Services);
        using HttpResponseMessage stale = await PostAsync(admin,
            $"/api/appointments/{created.Id}/complete",
            new ChangeStateRequest(updated.RowVersion, null));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.True(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "appointments.created"));
        Assert.Equal([300m, 500m], await dbContext.AppointmentServices
            .Where(item => item.AppointmentId == created.Id)
            .OrderBy(item => item.SequenceNumber)
            .Select(item => item.UnitPrice).ToArrayAsync());
    }

    private async Task<SeedData> SeedAsync()
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser admin = await users.FindByNameAsync("integration.admin") ??
            throw new InvalidOperationException("Missing integration admin.");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Department department = Department.Create($"قسم الحجوزات {suffix}", null, "الغرفة", now);
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();
        Specialization specialization = Specialization.Create(department.Id, "تخصص");
        Device device = Device.Create(department.Id, "جهاز مطلوب", $"REQ-{suffix}");
        Device optionalDevice = Device.Create(department.Id, "جهاز اختياري", $"OPT-{suffix}");
        dbContext.AddRange(specialization, device, optionalDevice);
        await dbContext.SaveChangesAsync();
        Service service = Service.Create(department.Id, specialization.Id, "جلسة",
            ServiceType.Session, 30, PricingMode.Fixed, 300m, admin.Id, now);
        Service alternativeService = Service.Create(department.Id, specialization.Id,
            "جلسة بديلة", ServiceType.Session, 45, PricingMode.Fixed, 500m, admin.Id, now);
        dbContext.Services.AddRange(service, alternativeService);
        await dbContext.SaveChangesAsync();
        service.ReplaceDeviceAssignments([(device, true), (optionalDevice, false)]);
        alternativeService.ReplaceDeviceAssignments([(optionalDevice, false)]);

        Doctor doctor = Doctor.Create(department.Id, "د. الحجز", null, now);
        Doctor alternativeDoctor = Doctor.Create(department.Id, "د. البديل", null, now);
        dbContext.Doctors.AddRange(doctor, alternativeDoctor);
        await dbContext.SaveChangesAsync();
        DoctorService doctorService = DoctorService.Create(doctor, service);
        DoctorService alternativeAssignment = DoctorService.Create(alternativeDoctor, service);
        DoctorService alternativeServiceAssignment = DoctorService.Create(
            alternativeDoctor, alternativeService);
        dbContext.DoctorServices.AddRange(doctorService, alternativeAssignment,
            alternativeServiceAssignment);

        DateOnly clinicDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        DateTimeOffset startAt = new(clinicDate.ToDateTime(new TimeOnly(10, 0)),
            TimeZoneInfo.Local.GetUtcOffset(clinicDate.ToDateTime(new TimeOnly(10, 0))));
        ClinicDayOfWeek day = (ClinicDayOfWeek)(((int)startAt.DayOfWeek + 6) % 7 + 1);
        dbContext.DoctorSchedules.Add(DoctorSchedule.Create(doctor.Id, day,
            new TimeOnly(9, 0), new TimeOnly(17, 0), clinicDate.AddDays(-1), null));
        dbContext.DoctorSchedules.Add(DoctorSchedule.Create(alternativeDoctor.Id, day,
            new TimeOnly(9, 0), new TimeOnly(17, 0), clinicDate.AddDays(-1), null));
        string phone = $"010{Random.Shared.Next(10000000, 99999999):D8}";
        Patient patient = Patient.Create("مريض الحجز", phone, null, null, 30,
            PatientGender.Male, "القاهرة", null, null, null, null, admin.Id,
            clinicDate, now);
        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();
        long optionalServiceDeviceId = alternativeService.DeviceAssignments.Single(item =>
            item.DeviceId == optionalDevice.Id).Id;
        return new(patient.Id, department.Id, service.Id, alternativeService.Id,
            doctor.Id, alternativeDoctor.Id, optionalServiceDeviceId, device.Id,
            clinicDate, startAt);
    }

    private async Task SetAppointmentStatusAsync(long appointmentId, AppointmentStatus status)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [appointments].[Appointments]
            SET [Status] = {(int)status}
            WHERE [Id] = {appointmentId};
            """);
    }

    private async Task SetPatientAgeObservationAsync(long patientId, int age,
        DateOnly recordedAt)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [patients].[Patients]
            SET [BirthDate] = NULL, [AgeAtRegistration] = {age}, [AgeRecordedAt] = {recordedAt}
            WHERE [Id] = {patientId};
            """);
    }

    private async Task AddMismatchedDeviceAsync(long appointmentServiceId, SeedData data,
        DateTimeOffset reservedFrom, DateTimeOffset reservedTo)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        _ = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [appointments].[AppointmentDevices]
                ([AppointmentServiceId], [ServiceDeviceId], [ServiceId], [DepartmentId],
                 [DeviceId], [ReservedFrom], [ReservedTo])
            VALUES ({appointmentServiceId}, {data.OptionalServiceDeviceId}, {data.AlternativeServiceId},
                {data.DepartmentId}, {data.RequiredDeviceId}, {reservedFrom}, {reservedTo});
            """);
    }

    private static async Task LoginAndChangePasswordAsync(HttpClient client)
    {
        using HttpResponseMessage login = await PostAsync(client, "/api/auth/login",
            new LoginRequest("integration.admin", IdentitySqlServerFixture.InitialAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage change = await PostAsync(client, "/api/auth/change-password",
            new ChangePasswordRequest(IdentitySqlServerFixture.InitialAdminPassword,
                IdentitySqlServerFixture.UpdatedAdminPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    }

    private static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Put, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(HttpClient client,
        string uri, TRequest request) => await SendAsync(client, HttpMethod.Post, uri, request);

    private static async Task<HttpResponseMessage> SendAsync<TRequest>(HttpClient client,
        HttpMethod method, string uri, TRequest request)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using HttpRequestMessage message = new(method, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        return await client.SendAsync(message);
    }

    private sealed record SeedData(long PatientId, long DepartmentId, long ServiceId,
        long AlternativeServiceId, long DoctorId, long AlternativeDoctorId,
        long OptionalServiceDeviceId, long RequiredDeviceId, DateOnly ClinicDate,
        DateTimeOffset StartAt);
    private sealed record CreateAppointmentRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services);
    private sealed record AppointmentLineRequest(long ServiceId, long DoctorId, int Quantity,
        IReadOnlyCollection<long> OptionalDeviceIds);
    private sealed record AppointmentResponse(long Id, AppointmentStatus Status,
        DateTimeOffset StartAt, DateTimeOffset EndAt, decimal NetAmount, string RowVersion,
        IReadOnlyCollection<AppointmentServiceResponse> Services);
    private sealed record AppointmentServiceResponse(long Id, long ServiceId);
    private sealed record AppointmentPageResponse(IReadOnlyCollection<AppointmentResponse> Items);
    private sealed record CheckAppointmentAvailabilityRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
        long? ExcludedAppointmentId);
    private sealed record AppointmentAvailabilityResponse(bool IsAvailable,
        IReadOnlyCollection<AvailableSlotResponse> Alternatives);
    private sealed record AvailableSlotResponse(DateTimeOffset StartAt, DateTimeOffset EndAt);
    private sealed record ChangeStateRequest(string RowVersion, string? Reason);
    private sealed record TransferDoctorRequest(long DoctorId, string Reason, string RowVersion);
    private sealed record UpdateAppointmentRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
        string RowVersion);
    private sealed record CreateClosureRequest(DateTimeOffset StartAt, DateTimeOffset EndAt,
        string Reason, bool ConfirmAffectedAppointments);
    private sealed record ClosureResponse(long Id, string RowVersion);
    private sealed record SchedulingVersionRequest(string RowVersion);
    private sealed record CreateDoctorExceptionRequest(DateOnly Date, TimeOnly? StartTime,
        TimeOnly? EndTime, DoctorExceptionType Type, string? Reason,
        bool ConfirmAffectedAppointments);
    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
