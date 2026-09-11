using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Patients;

public sealed class PatientFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public PatientFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PatientLifecycleEnforcesPrivacyUniquenessConcurrencyAndRoles()
    {
        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin, "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);

        using HttpResponseMessage excessivePage = await admin.GetAsync(
            "/api/patients?pageNumber=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.BadRequest, excessivePage.StatusCode);

        PatientResponse first = await CreatePatientAsync(admin, "أحمد الأول",
            "+20 1012345678", "01111111111");
        PatientResponse second = await CreatePatientAsync(admin, "منى الثانية",
            "01022222222", "01122222222");
        Assert.Equal("000001", first.FileNumber);
        Assert.Equal("000002", second.FileNumber);
        Assert.Equal("01012345678", first.PrimaryPhoneNumber);

        using HttpResponseMessage duplicate = await PostAsync(admin, "/api/patients",
            PatientRequest("مريض مكرر", "01012345678", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        ExistingPatientProblem duplicateBody = await duplicate.Content
            .ReadFromJsonAsync<ExistingPatientProblem>() ??
            throw new InvalidOperationException("Missing duplicate response.");
        Assert.Equal(first.Id, duplicateBody.ExistingPatient.Id);

        using HttpResponseMessage crossFieldDuplicate = await PostAsync(admin,
            "/api/patients", PatientRequest("مريض مكرر برقم ثانوي",
                "01111111111", null));
        Assert.Equal(HttpStatusCode.Conflict, crossFieldDuplicate.StatusCode);

        HttpResponseMessage[] competingPatients = await Task.WhenAll(
            PostAsync(admin, "/api/patients",
                PatientRequest("تنافس أساسي", "01077777777", null)),
            PostAsync(admin, "/api/patients",
                PatientRequest("تنافس ثانوي", "01088888888", "01077777777")));
        Assert.Single(competingPatients, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(competingPatients, item => item.StatusCode == HttpStatusCode.Conflict);
        foreach (HttpResponseMessage response in competingPatients)
        {
            response.Dispose();
        }

        PatientPageResponse<PatientResponse>? primarySearch = await admin.GetFromJsonAsync<
            PatientPageResponse<PatientResponse>>(
                "/api/patients?search=01012345678&pageNumber=1&pageSize=20");
        Assert.Single(primarySearch?.Items ?? []);
        PatientPageResponse<PatientResponse>? secondarySearch = await admin.GetFromJsonAsync<
            PatientPageResponse<PatientResponse>>(
                "/api/patients?search=01111111111&pageNumber=1&pageSize=20");
        Assert.Single(secondarySearch?.Items ?? []);

        await PostAndReadAsync<CreateSecretaryRequest, SecretaryResponse>(admin,
            "/api/admin/secretaries", new CreateSecretaryRequest("سكرتيرة المرضى",
                "01033333333", null, "patients.secretary", "SecretaryPass1"));
        using HttpClient secretary = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(secretary, "patients.secretary", "SecretaryPass1",
            "SecretaryPass2");

        SensitiveNoteReceiptResponse staffReceipt = await PostAndReadAsync<
            CreateNoteRequest, SensitiveNoteReceiptResponse>(secretary,
                $"/api/patients/{first.Id}/notes",
                new CreateNoteRequest("ملاحظة للموظفين", PatientNoteVisibility.Staff));
        SensitiveNoteReceiptResponse sensitiveReceipt = await PostAndReadAsync<
            CreateNoteRequest, SensitiveNoteReceiptResponse>(secretary,
                $"/api/patients/{first.Id}/notes",
                new CreateNoteRequest("سر طبي لا يظهر للسكرتيرة",
                    PatientNoteVisibility.AdminOnly));
        Assert.True(staffReceipt.Id > 0);
        Assert.True(sensitiveReceipt.Id > 0);

        PatientPageResponse<NoteResponse>? secretaryNotes = await secretary.GetFromJsonAsync<
            PatientPageResponse<NoteResponse>>(
                $"/api/patients/{first.Id}/notes?pageNumber=1&pageSize=20");
        Assert.Single(secretaryNotes?.Items ?? []);
        Assert.All(secretaryNotes!.Items,
            item => Assert.Equal(PatientNoteVisibility.Staff, item.Visibility));

        PatientPageResponse<NoteResponse>? adminNotes = await admin.GetFromJsonAsync<
            PatientPageResponse<NoteResponse>>(
                $"/api/admin/patients/{first.Id}/notes?pageNumber=1&pageSize=20");
        Assert.Equal(2, adminNotes?.TotalCount);
        NoteResponse sensitiveNote = Assert.Single(adminNotes!.Items,
            item => item.Visibility == PatientNoteVisibility.AdminOnly);

        TreatmentResponse treatment = await PostAndReadAsync<
            CreateTreatmentRequest, TreatmentResponse>(secretary,
            $"/api/patients/{first.Id}/treatment-history",
            new CreateTreatmentRequest(new DateOnly(2026, 9, 1), "جلسة علاج سابقة"));
        using HttpResponseMessage futureTreatment = await PostAsync(secretary,
            $"/api/patients/{first.Id}/treatment-history",
            new CreateTreatmentRequest(new DateOnly(2999, 1, 1), "تاريخ غير صحيح"));
        Assert.Equal(HttpStatusCode.BadRequest, futureTreatment.StatusCode);

        PatientTimelineResponse staffTimeline = await GetAndReadAsync<PatientTimelineResponse>(
            secretary, $"/api/patients/{first.Id}/timeline");
        Assert.Equal(2, staffTimeline?.TotalCount);
        Assert.DoesNotContain(staffTimeline!.Items,
            item => item.Summary.Contains("سر طبي", StringComparison.Ordinal));

        PatientTimelineResponse adminTimeline = await GetAndReadAsync<PatientTimelineResponse>(
            admin, $"/api/admin/patients/{first.Id}/timeline");
        Assert.Equal(3, adminTimeline?.TotalCount);
        Assert.Contains(adminTimeline!.Items,
            item => item.Summary.Contains("سر طبي", StringComparison.Ordinal));

        PatientTimelineResponse daylightSavingTimeline = await GetAndReadAsync<
            PatientTimelineResponse>(secretary, $"/api/patients/{first.Id}/timeline" +
                "?from=2026-04-24&to=2026-04-24");
        Assert.Empty(daylightSavingTimeline.Items);

        PatientResponse updated = await PutAndReadAsync<UpdatePatientRequest, PatientResponse>(
            secretary, $"/api/patients/{first.Id}", UpdateRequest(first, "أحمد المعدل"));
        using HttpResponseMessage staleUpdate = await PutAsync(secretary,
            $"/api/patients/{first.Id}", UpdateRequest(first, "تعديل قديم"));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        using HttpResponseMessage secretaryArchive = await PostAsync(secretary,
            $"/api/admin/patients/{first.Id}/archive",
            new ArchiveRequest("غير مسموح", updated.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, secretaryArchive.StatusCode);

        using HttpResponseMessage archiveNote = await PostAsync(admin,
            $"/api/admin/patients/notes/{sensitiveNote.Id}/archive",
            new ArchiveRequest("سجلت بالخطأ", sensitiveNote.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, archiveNote.StatusCode);
        using HttpResponseMessage archiveTreatment = await PostAsync(admin,
            $"/api/admin/patients/treatment-history/{treatment.Id}/archive",
            new ArchiveRequest("تصحيح السجل", treatment.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, archiveTreatment.StatusCode);
        using HttpResponseMessage archivePatient = await PostAsync(admin,
            $"/api/admin/patients/{first.Id}/archive",
            new ArchiveRequest("ملف تجريبي", updated.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, archivePatient.StatusCode);

        using HttpResponseMessage hiddenFromStaff = await secretary.GetAsync(
            $"/api/patients/{first.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenFromStaff.StatusCode);
        PatientResponse? archivedForAdmin = await admin.GetFromJsonAsync<PatientResponse>(
            $"/api/admin/patients/{first.Id}");
        Assert.True(archivedForAdmin?.IsArchived);

        using HttpResponseMessage noteOnArchivedPatient = await PostAsync(secretary,
            $"/api/patients/{first.Id}/notes",
            new CreateNoteRequest("لا يجب حفظها", PatientNoteVisibility.Staff));
        Assert.Equal(HttpStatusCode.NotFound, noteOnArchivedPatient.StatusCode);
        using HttpResponseMessage historyOnArchivedPatient = await PostAsync(secretary,
            $"/api/patients/{first.Id}/treatment-history",
            new CreateTreatmentRequest(new DateOnly(2026, 9, 1), "لا يجب حفظها"));
        Assert.Equal(HttpStatusCode.NotFound, historyOnArchivedPatient.StatusCode);

        using HttpResponseMessage stillReservedPhone = await PostAsync(admin, "/api/patients",
            PatientRequest("بعد الأرشفة", "01012345678", null));
        Assert.Equal(HttpStatusCode.Conflict, stillReservedPhone.StatusCode);

        PatientTimelineResponse archivedTimeline = await GetAndReadAsync<PatientTimelineResponse>(
            admin,
                $"/api/admin/patients/{first.Id}/timeline?includeArchived=true");
        Assert.Equal(3, archivedTimeline?.TotalCount);

        PatientResponse restored = await PostAndReadAsync<ArchiveRequest, PatientResponse>(
            admin, $"/api/admin/patients/{first.Id}/restore",
            new ArchiveRequest("إعادة فتح الملف", archivedForAdmin!.RowVersion));
        Assert.False(restored.IsArchived);
        PatientTimelineResponse restoredStaffTimeline = await GetAndReadAsync<PatientTimelineResponse>(
            secretary, $"/api/patients/{first.Id}/timeline");
        Assert.Single(restoredStaffTimeline?.Items ?? []);

        await VerifyAuditDoesNotCopySensitiveTextAsync();
        await VerifyDatabaseConstraintsAsync(first.Id);
    }

    private async Task VerifyAuditDoesNotCopySensitiveTextAsync()
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        string sensitiveText = "سر طبي لا يظهر للسكرتيرة";
        bool leaked = await dbContext.AuditLogs.AsNoTracking().AnyAsync(item =>
            (item.DataJson != null && item.DataJson.Contains(sensitiveText)) ||
            (item.Reason != null && item.Reason.Contains(sensitiveText)));
        Assert.False(leaked);
        Assert.True(await dbContext.AuditLogs.AsNoTracking().CountAsync(
            item => item.Action.StartsWith("patients.")) >= 8);
    }

    private async Task VerifyDatabaseConstraintsAsync(long patientId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        long actorUserId = await dbContext.Patients.Where(item => item.Id == patientId)
            .Select(item => item.CreatedByUserId).SingleAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [patients].[Patients] ([FullName], [PrimaryPhoneNumber], [BirthDate], [Gender], [IsArchived], [CreatedByUserId], [CreatedAt]) VALUES ({"هاتف مكرر"}, {"01012345678"}, {new DateOnly(1990, 1, 1)}, {1}, {false}, {actorUserId}, {now})"));

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [patients].[Patients] ([FullName], [PrimaryPhoneNumber], [SecondaryPhoneNumber], [AgeAtRegistration], [AgeRecordedAt], [Gender], [IsArchived], [CreatedByUserId], [CreatedAt]) VALUES ({"هاتف ثانوي مكرر"}, {"01066666666"}, {"01111111111"}, {25}, {new DateOnly(2026, 9, 6)}, {1}, {false}, {actorUserId}, {now})"));

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [patients].[Patients] ([FullName], [PrimaryPhoneNumber], [SecondaryPhoneNumber], [AgeAtRegistration], [AgeRecordedAt], [Gender], [IsArchived], [CreatedByUserId], [CreatedAt]) VALUES ({"رقمان متساويان"}, {"01044444444"}, {"01044444444"}, {25}, {new DateOnly(2026, 9, 6)}, {1}, {false}, {actorUserId}, {now})"));

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [patients].[Patients] ([FullName], [PrimaryPhoneNumber], [Gender], [IsArchived], [CreatedByUserId], [CreatedAt]) VALUES ({"دون عمر"}, {"01055555555"}, {1}, {false}, {actorUserId}, {now})"));
    }

    private static Task<PatientResponse> CreatePatientAsync(HttpClient client, string name,
        string primaryPhone, string? secondaryPhone) =>
        PostAndReadAsync<CreatePatientRequest, PatientResponse>(client, "/api/patients",
            PatientRequest(name, primaryPhone, secondaryPhone));

    private static CreatePatientRequest PatientRequest(string name, string primaryPhone,
        string? secondaryPhone) => new(name, primaryPhone, secondaryPhone, null, 30,
            PatientGender.Male, "مدينة نصر", "عنوان", null, null, null);

    private static UpdatePatientRequest UpdateRequest(PatientResponse patient, string name) =>
        new(name, patient.PrimaryPhoneNumber, patient.SecondaryPhoneNumber, null, 31,
            PatientGender.Male, "مدينة نصر", "عنوان جديد", null, null, null,
            patient.RowVersion);

    private static async Task LoginAndChangePasswordAsync(HttpClient client, string userName,
        string currentPassword, string newPassword)
    {
        await LoginAsync(client, userName, currentPassword);
        using HttpResponseMessage response = await PostAsync(client,
            "/api/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        using HttpResponseMessage response = await PostAsync(client, "/api/auth/login",
            new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(HttpClient client,
        string uri, TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from {uri}, received {(int)response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("The API response was empty.");
    }

    private static async Task<TResponse> GetAndReadAsync<TResponse>(HttpClient client,
        string uri)
    {
        using HttpResponseMessage response = await client.GetAsync(uri);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success from {uri}, received {(int)response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("The API response was empty.");
    }

    private static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(HttpClient client,
        string uri, TRequest request)
    {
        using HttpResponseMessage response = await PutAsync(client, uri, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("The API response was empty.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(HttpClient client,
        string uri, TRequest request)
    {
        string csrf = await GetCsrfTokenAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> PutAsync<TRequest>(HttpClient client,
        string uri, TRequest request)
    {
        string csrf = await GetCsrfTokenAsync(client);
        using HttpRequestMessage message = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        CsrfResponse? response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        return response?.Token ?? throw new InvalidOperationException("Missing CSRF token.");
    }

    private sealed record CsrfResponse(string Token);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CreateSecretaryRequest(string FullName, string PhoneNumber,
        string? Email, string UserName, string TemporaryPassword);
    private sealed record SecretaryResponse(long Id);
    private sealed record CreatePatientRequest(string FullName, string PrimaryPhoneNumber,
        string? SecondaryPhoneNumber, DateOnly? BirthDate, int? Age, PatientGender Gender,
        string? Area, string? Address, string? Email, string? GuardianName,
        string? GuardianPhoneNumber);
    private sealed record UpdatePatientRequest(string FullName, string PrimaryPhoneNumber,
        string? SecondaryPhoneNumber, DateOnly? BirthDate, int? Age, PatientGender Gender,
        string? Area, string? Address, string? Email, string? GuardianName,
        string? GuardianPhoneNumber, string RowVersion);
    private sealed record CreateNoteRequest(string NoteText, PatientNoteVisibility Visibility);
    private sealed record CreateTreatmentRequest(DateOnly EventDate, string Description);
    private sealed record ArchiveRequest(string Reason, string RowVersion);
    private sealed record SensitiveNoteReceiptResponse(long Id, DateTimeOffset CreatedAt);
    private sealed record PatientResponse(long Id, string FileNumber, string FullName,
        string PrimaryPhoneNumber, string? SecondaryPhoneNumber, bool IsArchived,
        string RowVersion);
    private sealed record NoteResponse(long Id, PatientNoteVisibility Visibility,
        string RowVersion);
    private sealed record TreatmentResponse(long Id, string RowVersion);
    private sealed record PatientPageResponse<T>(IReadOnlyCollection<T> Items, int TotalCount);
    private sealed record ExistingPatientProblem(ExistingPatientResponse ExistingPatient);
    private sealed record ExistingPatientResponse(long Id, string FileNumber,
        string FullName, int CurrentAge, bool IsArchived);
    private sealed record PatientTimelineResponse(
        IReadOnlyCollection<PatientTimelineItemResponse> Items, int TotalCount);
    private sealed record PatientTimelineItemResponse(PatientTimelineRecordType RecordType,
        string Summary);
}
