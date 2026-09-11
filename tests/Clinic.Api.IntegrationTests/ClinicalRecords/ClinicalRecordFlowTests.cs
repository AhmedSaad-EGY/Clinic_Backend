using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Domain.Appointments;
using Clinic.Domain.Catalog;
using Clinic.Domain.ClinicalRecords;
using Clinic.Domain.Patients;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.ClinicalRecords;

public sealed class ClinicalRecordFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public ClinicalRecordFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PrescriptionRevisionFollowUpAndBookingFlowIsAtomic()
    {
        _fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 9, 8, 8, 0, 0,
            TimeSpan.Zero));
        using HttpClient client = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(client);
        SeedData data = await SeedAsync();

        PrescriptionResponse created = await SendAndReadAsync<CreatePrescriptionRequest,
            PrescriptionResponse>(client, HttpMethod.Post, "/api/prescriptions",
            new(data.CompletedAppointmentServiceId,
                Content(new DateOnly(2026, 9, 20))), HttpStatusCode.Created);
        Assert.Equal(PrescriptionStatus.Draft, created.Status);

        using HttpResponseMessage duplicate = await SendAsync(client, HttpMethod.Post,
            "/api/prescriptions", new CreatePrescriptionRequest(
                data.CompletedAppointmentServiceId, Content(new DateOnly(2026, 9, 20))));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        PrescriptionResponse finalized = await SendAndReadAsync<FinalizeRequest,
            PrescriptionResponse>(client, HttpMethod.Post,
            $"/api/prescriptions/{created.Id}/finalize",
            new(true, created.RowVersion), HttpStatusCode.OK);
        Assert.Equal(PrescriptionStatus.Finalized, finalized.Status);

        DateOnly correctedDate = DateOnly.FromDateTime(data.BookingStart.Date);
        PrescriptionResponse corrected = await SendAndReadAsync<CorrectRequest,
            PrescriptionResponse>(client, HttpMethod.Post,
            $"/api/admin/prescriptions/{created.Id}/revisions",
            new(Content(correctedDate), "تصحيح موعد الإعادة", true,
                finalized.RowVersion), HttpStatusCode.Created);
        Assert.Equal(2, corrected.CurrentRevision.RevisionNumber);

        FollowUpPage followUps = await client.GetFromJsonAsync<FollowUpPage>(
            "/api/follow-ups?status=1") ?? throw new InvalidOperationException();
        FollowUpResponse followUp = Assert.Single(followUps.Items);
        Assert.Equal(correctedDate, followUp.ReturnDate);

        AppointmentResponse booked = await SendAndReadAsync<CreateAppointmentRequest,
            AppointmentResponse>(client, HttpMethod.Post, "/api/appointments",
            new(data.PatientId, data.DepartmentId, data.BookingStart,
                [new(data.ServiceId, data.DoctorId, 1, [])],
                new(followUp.Id, followUp.RowVersion)), HttpStatusCode.OK);

        FollowUpResponse completed = await client.GetFromJsonAsync<FollowUpResponse>(
            $"/api/follow-ups/{followUp.Id}") ?? throw new InvalidOperationException();
        Assert.Equal(FollowUpStatus.Completed, completed.Status);
        Assert.Equal(booked.Id, completed.ResultAppointmentId);

        PatientTimelineResponse timeline = await client.GetFromJsonAsync<
            PatientTimelineResponse>($"/api/patients/{data.PatientId}/timeline" +
                "?recordTypes=6&recordTypes=7") ?? throw new InvalidOperationException();
        Assert.Contains(timeline.Items,
            item => item.RecordType == PatientTimelineRecordType.Prescription);
        Assert.Contains(timeline.Items,
            item => item.RecordType == PatientTimelineRecordType.FollowUp);

        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.Equal(2, await dbContext.PrescriptionRevisions.CountAsync(item =>
            item.PrescriptionId == created.Id));
        Assert.True(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "FollowUpConvertedToAppointment"));
        Assert.False(await dbContext.AuditLogs.AnyAsync(item =>
            item.Reason != null && item.Reason.Contains("دواء اختبار")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FollowUpRequiresExistingPatientAndDepartment(bool invalidPatient)
    {
        SeedData data = await SeedAsync();
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser admin = await users.FindByNameAsync("integration.admin") ??
            throw new InvalidOperationException();
        DateTimeOffset now = _fixture.Clock.GetUtcNow();
        Prescription prescription = Prescription.Create(data.CompletedAppointmentServiceId,
            admin.Id, now, new PrescriptionContent(null,
            [("دواء اختبار", 1m, "قرص", 1, null, null, null, null)]));
        dbContext.Prescriptions.Add(prescription);
        await dbContext.SaveChangesAsync();
        prescription.SelectCurrentRevision(prescription.Revisions.Single());
        await dbContext.SaveChangesAsync();

        const long missingId = 9_000_000_000;
        dbContext.FollowUps.Add(FollowUp.Create(prescription.Id,
            invalidPatient ? missingId : data.PatientId,
            invalidPatient ? data.DepartmentId : missingId,
            new DateOnly(2026, 9, 20), admin.Id, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private async Task<SeedData> SeedAsync()
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser admin = await users.FindByNameAsync("integration.admin") ??
            throw new InvalidOperationException();
        DateTimeOffset now = new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Department department = Department.Create($"قسم روشتات {suffix}", null,
            "الغرفة", now);
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();
        Specialization specialization = Specialization.Create(department.Id, "تخصص");
        dbContext.Specializations.Add(specialization);
        await dbContext.SaveChangesAsync();
        Service service = Service.Create(department.Id, specialization.Id, "كشف",
            ServiceType.Consultation, 30, PricingMode.Fixed, 250m, admin.Id, now);
        Doctor doctor = Doctor.Create(department.Id, "د. الروشتة", null, now);
        dbContext.AddRange(service, doctor);
        await dbContext.SaveChangesAsync();
        DoctorService assignment = DoctorService.Create(doctor, service);
        dbContext.DoctorServices.Add(assignment);
        Patient patient = Patient.Create("مريض الروشتة",
            $"010{Random.Shared.Next(10000000, 99999999):D8}", null, null, 35,
            PatientGender.Male, "القاهرة", null, null, null, null, admin.Id,
            new DateOnly(2026, 9, 8), now);
        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();

        Appointment completed = Appointment.Create(patient.Id, department.Room.Id,
            department.Id, now.AddDays(-1), false, admin.Id, now.AddDays(-2));
        AppointmentService line = completed.AddService(service.Id, assignment.Id,
            service.DurationMinutes, 1, service.CurrentUnitPrice, []);
        completed.Complete(admin.Id, now.AddDays(-1).AddMinutes(30));
        dbContext.Appointments.Add(completed);

        DateTimeOffset bookingStart = now.AddDays(12).AddHours(2);
        ClinicDayOfWeek day = (ClinicDayOfWeek)(((int)bookingStart.DayOfWeek + 6) % 7 + 1);
        dbContext.DoctorSchedules.Add(DoctorSchedule.Create(doctor.Id, day,
            new TimeOnly(8, 0), new TimeOnly(18, 0),
            DateOnly.FromDateTime(now.Date), null));
        await dbContext.SaveChangesAsync();
        return new(patient.Id, department.Id, service.Id, doctor.Id, line.Id,
            bookingStart);
    }

    private static PrescriptionContentRequest Content(DateOnly returnDate) =>
        new([new("دواء اختبار", 1m, "قرص", 2, null, "5 أيام",
            FoodTiming.AfterMeal, "بعد الطعام")], returnDate, null);

    private static async Task LoginAndChangePasswordAsync(HttpClient client)
    {
        using HttpResponseMessage login = await SendAsync(client, HttpMethod.Post,
            "/api/auth/login", new LoginRequest("integration.admin",
                IdentitySqlServerFixture.InitialAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage change = await SendAsync(client, HttpMethod.Post,
            "/api/auth/change-password", new ChangePasswordRequest(
                IdentitySqlServerFixture.InitialAdminPassword,
                IdentitySqlServerFixture.UpdatedAdminPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    }

    private static async Task<TResponse> SendAndReadAsync<TRequest, TResponse>(
        HttpClient client, HttpMethod method, string uri, TRequest request,
        HttpStatusCode expected)
    {
        using HttpResponseMessage response = await SendAsync(client, method, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException(body);
    }

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
        long DoctorId, long CompletedAppointmentServiceId, DateTimeOffset BookingStart);
    private sealed record PrescriptionItemRequest(string MedicineName, decimal? DoseAmount,
        string? DoseUnit, int? TimesPerDay, string? FrequencyText, string? DurationText,
        FoodTiming? FoodTiming, string? Instructions);
    private sealed record PrescriptionContentRequest(
        IReadOnlyCollection<PrescriptionItemRequest> Items, DateOnly? ReturnDate,
        int? ReturnAfterDays);
    private sealed record CreatePrescriptionRequest(long AppointmentServiceId,
        PrescriptionContentRequest Content);
    private sealed record FinalizeRequest(bool MatchesDoctorPrescription, string RowVersion);
    private sealed record CorrectRequest(PrescriptionContentRequest Content, string Reason,
        bool MatchesDoctorPrescription, string RowVersion);
    private sealed record PrescriptionRevisionResponse(int RevisionNumber);
    private sealed record PrescriptionResponse(long Id, PrescriptionStatus Status,
        PrescriptionRevisionResponse CurrentRevision, string RowVersion);
    private sealed record FollowUpPage(IReadOnlyCollection<FollowUpResponse> Items);
    private sealed record FollowUpResponse(long Id, DateOnly ReturnDate,
        FollowUpStatus Status, long? ResultAppointmentId, string RowVersion);
    private sealed record PatientTimelineResponse(
        IReadOnlyCollection<PatientTimelineItemResponse> Items);
    private sealed record PatientTimelineItemResponse(PatientTimelineRecordType RecordType);
    private sealed record FollowUpBookingRequest(long FollowUpId, string RowVersion);
    private sealed record AppointmentLineRequest(long ServiceId, long DoctorId, int Quantity,
        IReadOnlyCollection<long> OptionalDeviceIds);
    private sealed record CreateAppointmentRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
        FollowUpBookingRequest FollowUp);
    private sealed record AppointmentResponse(long Id);
    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
