using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Domain.Cashier;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Cashier;

public sealed class ShiftFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public ShiftFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ShiftFlowEnforcesDrawerOwnershipOverlapAndReconciliation()
    {
        DateOnly clinicDate = new(2026, 9, 20);
        Assert.Equal(ClinicDayOfWeek.Sunday, ClinicWeekday(clinicDate));
        DateTimeOffset scheduledStart = ClinicInstant(clinicDate, new TimeOnly(10, 0));
        _fixture.Clock.SetUtcNow(scheduledStart.AddHours(-1));
        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin,
            "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);
        using (HttpResponseMessage openApi = await admin.GetAsync(
            "/swagger/v1/swagger.json"))
        {
            string document = await openApi.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
            Assert.Contains("/api/admin/cashier/shifts/generate", document);
            Assert.Contains("/api/cashier/shifts/current", document);
        }
        SecretaryResponse secretary = await CreateSecretaryAsync(admin);
        await AssertDrawerCreatedAsync(secretary.Id);
        ShiftPolicyResponse policy = await GetAsync<ShiftPolicyResponse>(admin,
            "/api/admin/cashier/shift-policy");
        Assert.Equal(10, policy.ClosingGraceMinutes);
        CashDrawerPageResponse drawers = await GetAsync<CashDrawerPageResponse>(admin,
            "/api/admin/cashier/drawers");
        Assert.Contains(drawers.Items, item => item.SecretaryUserId == secretary.Id);

        GenerateShiftsRequest generation = new(
            secretary.Id,
            clinicDate,
            clinicDate,
            [ClinicWeekday(clinicDate)],
            new TimeOnly(10, 0),
            new TimeOnly(14, 0));
        GeneratedShiftsResponse created = await PostAndReadAsync<GenerateShiftsRequest,
            GeneratedShiftsResponse>(admin, "/api/admin/cashier/shifts/generate", generation);
        ShiftResponse shift = Assert.Single(created.Items);
        Assert.Equal(ShiftStatus.Scheduled, shift.Status);
        Assert.Equal(scheduledStart.AddHours(4).AddMinutes(10), shift.GraceEndsAt);

        using HttpResponseMessage graceOverlap = await PostAsync(admin,
            "/api/admin/cashier/shifts/generate", generation with
            {
                StartTime = new TimeOnly(14, 5),
                EndTime = new TimeOnly(18, 0)
            });
        Assert.Equal(HttpStatusCode.Conflict, graceOverlap.StatusCode);
        Assert.Equal(1, await CountShiftsAsync(secretary.Id));

        using HttpResponseMessage atomicConflict = await PostAsync(admin,
            "/api/admin/cashier/shifts/generate", generation with
            {
                ToDate = clinicDate.AddDays(1),
                DaysOfWeek = [ClinicWeekday(clinicDate),
                    ClinicWeekday(clinicDate.AddDays(1))]
            });
        Assert.Equal(HttpStatusCode.Conflict, atomicConflict.StatusCode);
        Assert.Equal(1, await CountShiftsAsync(secretary.Id));

        GenerateShiftsRequest concurrentRequest = generation with
        {
            FromDate = clinicDate.AddDays(2),
            ToDate = clinicDate.AddDays(2),
            DaysOfWeek = [ClinicWeekday(clinicDate.AddDays(2))]
        };
        HttpResponseMessage[] concurrentResponses = await Task.WhenAll(
            PostAsync(admin, "/api/admin/cashier/shifts/generate", concurrentRequest),
            PostAsync(admin, "/api/admin/cashier/shifts/generate", concurrentRequest));
        Assert.Single(concurrentResponses,
            response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(concurrentResponses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (HttpResponseMessage response in concurrentResponses)
        {
            response.Dispose();
        }
        Assert.Equal(2, await CountShiftsAsync(secretary.Id));

        SecretaryResponse secondSecretary = await CreateSecretaryAsync(admin);
        GeneratedShiftsResponse overlappingOtherSecretary =
            await PostAndReadAsync<GenerateShiftsRequest, GeneratedShiftsResponse>(
                admin, "/api/admin/cashier/shifts/generate",
                generation with { SecretaryUserId = secondSecretary.Id });
        Assert.Single(overlappingOtherSecretary.Items);

        DateOnly futureDate = clinicDate.AddDays(3);
        GeneratedShiftsResponse futureCreated =
            await PostAndReadAsync<GenerateShiftsRequest, GeneratedShiftsResponse>(
                admin, "/api/admin/cashier/shifts/generate", generation with
                {
                    FromDate = futureDate,
                    ToDate = futureDate,
                    DaysOfWeek = [ClinicWeekday(futureDate)]
                });
        ShiftResponse futureShift = Assert.Single(futureCreated.Items);
        ShiftResponse updatedFuture = await PutAndReadAsync<UpdateShiftRequest,
            ShiftResponse>(admin, $"/api/admin/cashier/shifts/{futureShift.Id}",
                new UpdateShiftRequest(futureDate, new TimeOnly(11, 0),
                    new TimeOnly(15, 0), futureShift.RowVersion));
        using HttpResponseMessage cancelled = await PostAsync(admin,
            $"/api/admin/cashier/shifts/{futureShift.Id}/cancel",
            new CancelShiftRequest("تغيير جدول العمل", updatedFuture.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        ShiftResponse cancelledShift = await GetAsync<ShiftResponse>(admin,
            $"/api/admin/cashier/shifts/{futureShift.Id}");
        Assert.Equal(ShiftStatus.Cancelled, cancelledShift.Status);
        GeneratedShiftsResponse replacement =
            await PostAndReadAsync<GenerateShiftsRequest, GeneratedShiftsResponse>(
                admin, "/api/admin/cashier/shifts/generate", generation with
                {
                    FromDate = futureDate,
                    ToDate = futureDate,
                    DaysOfWeek = [ClinicWeekday(futureDate)],
                    StartTime = new TimeOnly(11, 0),
                    EndTime = new TimeOnly(15, 0)
                });
        Assert.Single(replacement.Items);

        using HttpClient secretaryClient = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(secretaryClient, secretary.UserName,
            "SecretaryPass1", "SecretaryPass2");
        ShiftResponse upcoming = await GetAsync<ShiftResponse>(secretaryClient,
            "/api/cashier/shifts/current");
        Assert.Equal(ShiftStatus.Scheduled, upcoming.Status);
        Assert.Equal(shift.Id, upcoming.Id);

        using HttpResponseMessage wrongOwner = await PostAsync(admin,
            $"/api/cashier/shifts/{shift.Id}/opening-balance",
            new OpeningBalanceRequest(100, shift.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, wrongOwner.StatusCode);

        _fixture.Clock.SetUtcNow(scheduledStart);
        ShiftResponse extended = await PostAndReadAsync<ExtendRequest, ShiftResponse>(admin,
            $"/api/admin/cashier/shifts/{shift.Id}/extend",
            new ExtendRequest(scheduledStart.AddHours(4).AddMinutes(30),
                "استمرار العمل", shift.RowVersion));
        Assert.Equal(scheduledStart.AddHours(4).AddMinutes(40), extended.GraceEndsAt);
        GeneratedShiftsResponse nextCreated =
            await PostAndReadAsync<GenerateShiftsRequest, GeneratedShiftsResponse>(
                admin, "/api/admin/cashier/shifts/generate", generation with
                {
                    StartTime = new TimeOnly(14, 40),
                    EndTime = new TimeOnly(18, 0)
                });
        ShiftResponse nextShift = Assert.Single(nextCreated.Items);

        ShiftResponse current = await GetAsync<ShiftResponse>(secretaryClient,
            "/api/cashier/shifts/current");
        Assert.Equal(ShiftStatus.Open, current.Status);
        Assert.True(current.CanEnterOpeningBalance);
        Assert.False(current.CanCollect);
        Assert.False(current.CanReconcile);
        Assert.False(current.CanClose);

        ShiftResponse opened = await PostAndReadAsync<OpeningBalanceRequest,
            ShiftResponse>(secretaryClient,
                $"/api/cashier/shifts/{shift.Id}/opening-balance",
                new OpeningBalanceRequest(100, current.RowVersion));
        Assert.True(opened.CanCollect);
        Assert.False(opened.CanReconcile);
        ShiftResponse adminView = await GetAsync<ShiftResponse>(admin,
            $"/api/admin/cashier/shifts/{shift.Id}");
        Assert.True(adminView.CanReconcile);
        ShiftResponse earlyAdminReconciliation =
            await PostAndReadAsync<ReconcileRequest, ShiftResponse>(admin,
                $"/api/admin/cashier/shifts/{shift.Id}/reconcile",
                new ReconcileRequest(100, adminView.RowVersion,
                    "اختبار المطابقة الإدارية"));
        Assert.True(earlyAdminReconciliation.CanClose);
        using HttpResponseMessage staleOpening = await PostAsync(secretaryClient,
            $"/api/cashier/shifts/{shift.Id}/opening-balance",
            new OpeningBalanceRequest(100, current.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleOpening.StatusCode);

        _fixture.Clock.SetUtcNow(scheduledStart.AddHours(4).AddMinutes(41));
        ShiftResponse overdue = await GetAsync<ShiftResponse>(secretaryClient,
            "/api/cashier/shifts/current");
        Assert.Equal(ShiftStatus.Grace, overdue.Status);
        Assert.True(overdue.IsGraceExpired);
        Assert.False(overdue.CanCollect);
        using HttpResponseMessage previousStillOpen = await PostAsync(secretaryClient,
            $"/api/cashier/shifts/{nextShift.Id}/opening-balance",
            new OpeningBalanceRequest(0, nextShift.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, previousStillOpen.StatusCode);

        ShiftResponse reconciled = await PostAndReadAsync<ReconcileRequest,
            ShiftResponse>(secretaryClient,
                $"/api/cashier/shifts/{shift.Id}/reconcile",
                new ReconcileRequest(95, overdue.RowVersion));
        Assert.Equal(-5, reconciled.CashVariance);
        using HttpResponseMessage missingVarianceReason = await PostAsync(secretaryClient,
            $"/api/cashier/shifts/{shift.Id}/close",
            new CloseRequest(reconciled.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, missingVarianceReason.StatusCode);
        ShiftResponse closed = await PostAndReadAsync<CloseRequest,
            ShiftResponse>(secretaryClient,
                $"/api/cashier/shifts/{shift.Id}/close",
                new CloseRequest(reconciled.RowVersion, "عجز خمسة جنيهات"));
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(-5, closed.CashVariance);
        ShiftResponse nextCurrent = await GetAsync<ShiftResponse>(secretaryClient,
            "/api/cashier/shifts/current");
        Assert.Equal(nextShift.Id, nextCurrent.Id);
        ShiftResponse nextOpened = await PostAndReadAsync<OpeningBalanceRequest,
            ShiftResponse>(secretaryClient,
                $"/api/cashier/shifts/{nextShift.Id}/opening-balance",
                new OpeningBalanceRequest(0, nextCurrent.RowVersion));
        Assert.True(nextOpened.CanCollect);

        ShiftPageResponse closedSearch = await GetAsync<ShiftPageResponse>(admin,
            "/api/admin/cashier/shifts?status=4");
        Assert.Contains(closedSearch.Items, item => item.Id == shift.Id);
        ShiftPolicyResponse updatedPolicy = await PutAndReadAsync<UpdatePolicyRequest,
            ShiftPolicyResponse>(admin, "/api/admin/cashier/shift-policy",
                new UpdatePolicyRequest(15, policy.RowVersion));
        Assert.Equal(15, updatedPolicy.ClosingGraceMinutes);
        using HttpResponseMessage stalePolicy = await PutAsync(admin,
            "/api/admin/cashier/shift-policy",
            new UpdatePolicyRequest(20, policy.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, stalePolicy.StatusCode);

        await AssertFinancialAuditAsync(shift.Id);
        await AssertDatabaseConstraintsAsync(secretary.Id, replacement.Items.Single().Id);
    }

    private static async Task<SecretaryResponse> CreateSecretaryAsync(HttpClient admin)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        return await PostAndReadAsync<CreateSecretaryRequest, SecretaryResponse>(admin,
            "/api/admin/secretaries", new CreateSecretaryRequest(
                "سكرتيرة الشيفت",
                $"010{Random.Shared.Next(10000000, 99999999):D8}",
                null,
                $"shift.secretary.{suffix}",
                "SecretaryPass1"));
    }

    private async Task AssertDrawerCreatedAsync(long secretaryId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.True(await dbContext.CashDrawers.AnyAsync(item =>
            item.SecretaryUserId == secretaryId && item.IsActive));
    }

    private async Task<int> CountShiftsAsync(long secretaryId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        return await dbContext.Shifts.CountAsync(item =>
            item.CashDrawer.SecretaryUserId == secretaryId);
    }

    private async Task AssertFinancialAuditAsync(long shiftId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        string id = shiftId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] actions = await dbContext.AuditLogs.AsNoTracking()
            .Where(item => item.EntityType == nameof(Shift) && item.EntityId == id)
            .Select(item => item.Action)
            .ToArrayAsync();
        Assert.Contains("cashier.shift_created", actions);
        Assert.Contains("cashier.opening_balance_recorded", actions);
        Assert.Contains("cashier.shift_reconciled", actions);
        Assert.Contains("cashier.shift_closed", actions);
    }

    private async Task AssertDatabaseConstraintsAsync(long secretaryId, long scheduledShiftId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [cashier].[CashDrawers]
                    ([SecretaryUserId], [Name], [IsActive], [CreatedAt])
                VALUES ({secretaryId}, N'درج مكرر', 1, {DateTimeOffset.UtcNow});
                """));
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [cashier].[Shifts]
                SET [Status] = 4
                WHERE [Id] = {scheduledShiftId};
                """));
    }

    private static DateTimeOffset ClinicInstant(DateOnly date, TimeOnly time)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        DateTime local = DateTime.SpecifyKind(date.ToDateTime(time),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static ClinicDayOfWeek ClinicWeekday(DateOnly date) =>
        (ClinicDayOfWeek)(((int)date.DayOfWeek + 6) % 7 + 1);

    private static async Task LoginAndChangePasswordAsync(HttpClient client,
        string userName, string currentPassword, string newPassword)
    {
        using HttpResponseMessage login = await PostAsync(client, "/api/auth/login",
            new LoginRequest(userName, currentPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage change = await PostAsync(client,
            "/api/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    }

    private static async Task<TResponse> GetAsync<TResponse>(HttpClient client, string uri)
    {
        using HttpResponseMessage response = await client.GetAsync(uri);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(
        HttpClient client, string uri, TRequest request)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using HttpRequestMessage message = new(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        return await client.SendAsync(message);
    }

    private static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await PutAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<HttpResponseMessage> PutAsync<TRequest>(
        HttpClient client, string uri, TRequest request)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using HttpRequestMessage message = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        return await client.SendAsync(message);
    }

    private sealed record GenerateShiftsRequest(long SecretaryUserId, DateOnly FromDate,
        DateOnly ToDate, IReadOnlyCollection<ClinicDayOfWeek> DaysOfWeek,
        TimeOnly StartTime, TimeOnly EndTime);
    private sealed record GeneratedShiftsResponse(IReadOnlyCollection<ShiftResponse> Items);
    private sealed record ShiftResponse(long Id, DateTimeOffset GraceEndsAt,
        ShiftStatus Status, bool IsGraceExpired, bool CanEnterOpeningBalance,
        bool CanCollect, bool CanReconcile, bool CanClose,
        decimal? CashVariance, string RowVersion);
    private sealed record OpeningBalanceRequest(decimal Amount, string RowVersion);
    private sealed record ReconcileRequest(decimal DeclaredCash, string RowVersion,
        string? Reason = null);
    private sealed record CloseRequest(string RowVersion, string? Reason = null);
    private sealed record ExtendRequest(DateTimeOffset NewScheduledEnd,
        string Reason, string RowVersion);
    private sealed record UpdateShiftRequest(DateOnly Date, TimeOnly StartTime,
        TimeOnly EndTime, string RowVersion);
    private sealed record CancelShiftRequest(string Reason, string RowVersion);
    private sealed record ShiftPolicyResponse(int ClosingGraceMinutes,
        string RowVersion);
    private sealed record UpdatePolicyRequest(int ClosingGraceMinutes,
        string RowVersion);
    private sealed record CashDrawerPageResponse(
        IReadOnlyCollection<CashDrawerResponse> Items);
    private sealed record CashDrawerResponse(long SecretaryUserId);
    private sealed record ShiftPageResponse(IReadOnlyCollection<ShiftResponse> Items);
    private sealed record CreateSecretaryRequest(string FullName, string PhoneNumber,
        string? Email, string UserName, string TemporaryPassword);
    private sealed record SecretaryResponse(long Id, string UserName);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CsrfResponse(string Token);
}
