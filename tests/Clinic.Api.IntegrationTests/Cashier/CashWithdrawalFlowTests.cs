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

public sealed class CashWithdrawalFlowTests
    : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public CashWithdrawalFlowTests(IdentitySqlServerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task WithdrawalRequiresApprovalAndUpdatesExpectedCashAtomically()
    {
        DateOnly clinicDate = new(2026, 10, 4);
        DateTimeOffset start = ClinicInstant(clinicDate, new TimeOnly(10, 0));
        _fixture.Clock.SetUtcNow(start.AddHours(-1));

        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin, "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);
        using (HttpResponseMessage openApi = await admin.GetAsync(
            "/swagger/v1/swagger.json"))
        {
            string document = await openApi.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
            Assert.Contains("/api/cashier/cash-withdrawals", document);
            Assert.Contains(
                "/api/admin/cashier/cash-withdrawals/{withdrawalId}/approve",
                document);
            Assert.Contains("/api/admin/reports/financial", document);
            Assert.Contains("/api/admin/audit-logs", document);
        }

        SecretaryResponse secretary = await CreateSecretaryAsync(admin);
        ShiftResponse shift = Assert.Single((await PostAndReadAsync<
            GenerateShiftsRequest, GeneratedShiftsResponse>(admin,
            "/api/admin/cashier/shifts/generate", new GenerateShiftsRequest(
                secretary.Id, clinicDate, clinicDate,
                [ClinicWeekday(clinicDate)], new TimeOnly(10, 0),
                new TimeOnly(14, 0)))).Items);

        using HttpClient cashier = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(cashier, secretary.UserName,
            "SecretaryPass1", "SecretaryPass2");
        _fixture.Clock.SetUtcNow(start);
        ShiftResponse current = await GetAsync<ShiftResponse>(cashier,
            "/api/cashier/shifts/current");
        await PostAndReadAsync<OpeningBalanceRequest, ShiftResponse>(cashier,
            $"/api/cashier/shifts/{shift.Id}/opening-balance",
            new OpeningBalanceRequest(100, current.RowVersion));

        using (HttpResponseMessage adminCannotRequest = await PostAsync(admin,
            "/api/cashier/cash-withdrawals",
            new CreateWithdrawalRequest(5, "طلب غير مسموح"), Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                adminCannotRequest.StatusCode);
        }

        Guid requestKey = Guid.NewGuid();
        CreateWithdrawalRequest createRequest = new(40, "شراء مستلزمات");
        using HttpResponseMessage createdResponse = await PostAsync(cashier,
            "/api/cashier/cash-withdrawals", createRequest, requestKey);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        WithdrawalResponse requested = await ReadAsync<WithdrawalResponse>(
            createdResponse);
        Assert.StartsWith("WDL-20261004-", requested.WithdrawalNumber,
            StringComparison.Ordinal);
        Assert.Equal(CashWithdrawalStatus.Pending, requested.Status);

        using (HttpResponseMessage replayResponse = await PostAsync(cashier,
            "/api/cashier/cash-withdrawals", createRequest, requestKey))
        {
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            Assert.Equal("true", replayResponse.Headers
                .GetValues("Idempotency-Replayed").Single());
        }

        using (HttpResponseMessage changedReplay = await PostAsync(cashier,
            "/api/cashier/cash-withdrawals", createRequest with { Amount = 41 },
            requestKey))
        {
            Assert.Equal(HttpStatusCode.Conflict, changedReplay.StatusCode);
        }

        WithdrawalPageResponse pending = await GetAsync<WithdrawalPageResponse>(
            admin, "/api/admin/cashier/cash-withdrawals?status=1");
        Assert.Contains(pending.Items, item => item.Id == requested.Id);

        WithdrawalResponse approved = await PostAndReadAsync<ReviewRequest,
            WithdrawalResponse>(admin,
            $"/api/admin/cashier/cash-withdrawals/{requested.Id}/approve",
            new ReviewRequest("تمت مراجعة المصروف", requested.RowVersion));
        Assert.Equal(CashWithdrawalStatus.Approved, approved.Status);

        using (HttpResponseMessage cannotCancel = await PostAsync(cashier,
            $"/api/cashier/cash-withdrawals/{requested.Id}/cancel",
            new RowVersionRequest(approved.RowVersion)))
        {
            Assert.Equal(HttpStatusCode.Conflict, cannotCancel.StatusCode);
        }

        ShiftResponse adminShift = await GetAsync<ShiftResponse>(admin,
            $"/api/admin/cashier/shifts/{shift.Id}");
        ShiftResponse reconciled = await PostAndReadAsync<ReconcileRequest,
            ShiftResponse>(admin,
            $"/api/admin/cashier/shifts/{shift.Id}/reconcile",
            new ReconcileRequest(100, adminShift.RowVersion,
                "مطابقة قبل السحب"));
        Assert.Equal(100, reconciled.ExpectedCash);

        Guid executionKey = Guid.NewGuid();
        HttpResponseMessage[] executions = await Task.WhenAll(
            PostAsync(cashier,
                $"/api/cashier/cash-withdrawals/{requested.Id}/execute",
                new RowVersionRequest(approved.RowVersion), executionKey),
            PostAsync(cashier,
                $"/api/cashier/cash-withdrawals/{requested.Id}/execute",
                new RowVersionRequest(approved.RowVersion), executionKey));
        Assert.All(executions, response =>
            Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Single(executions, response => response.Headers.Contains(
            "Idempotency-Replayed"));
        ExecutedWithdrawalResponse executed = await ReadAsync<
            ExecutedWithdrawalResponse>(executions[0]);
        foreach (HttpResponseMessage response in executions)
        {
            response.Dispose();
        }

        Assert.Equal(CashWithdrawalStatus.Executed,
            executed.Withdrawal.Status);
        Assert.Equal(60, executed.CurrentExpectedCash);
        ShiftSummaryResponse summary = await GetAsync<ShiftSummaryResponse>(cashier,
            $"/api/cashier/shifts/{shift.Id}/collection-summary");
        Assert.Equal(40, summary.CashWithdrawn);
        Assert.Equal(60, summary.CurrentExpectedCash);
        Assert.Equal(1, summary.ExecutedWithdrawalCount);
        Assert.Equal(0, summary.PendingWithdrawalCount);
        Assert.Null(summary.ReconciledExpectedCash);

        DashboardResponse dashboard = await GetAsync<DashboardResponse>(admin,
            "/api/admin/dashboard/summary?reportDate=2026-10-04");
        Assert.Equal(40, dashboard.CashWithdrawn);
        FinancialReportResponse financial = await GetAsync<FinancialReportResponse>(
            admin, "/api/admin/reports/financial?from=2026-10-04&to=2026-10-04");
        Assert.Equal(40, financial.CashWithdrawn);
        OperationalReportResponse operations = await GetAsync<
            OperationalReportResponse>(admin,
            "/api/admin/reports/operations?from=2026-10-04&to=2026-10-04");
        Assert.Equal(0, operations.AppointmentCount);
        ComparisonReportResponse comparison = await GetAsync<
            ComparisonReportResponse>(admin,
            "/api/admin/reports/comparison?from=2026-10-04&to=2026-10-04" +
            "&groupBy=1");
        Assert.Empty(comparison.Points);
        ShiftReportResponse shiftReport = await GetAsync<ShiftReportResponse>(admin,
            "/api/admin/reports/shifts?from=2026-10-04&to=2026-10-04");
        ShiftReportItemResponse reportItem = Assert.Single(shiftReport.Items,
            item => item.ShiftId == shift.Id);
        Assert.Equal(60, reportItem.ExpectedCash);
        AuditReportResponse auditReport = await GetAsync<AuditReportResponse>(admin,
            "/api/admin/audit-logs?from=2026-10-04&to=2026-10-04" +
            "&action=cashier.cash_withdrawal_executed");
        Assert.Contains(auditReport.Items, item =>
            item.EntityId == requested.Id.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        using (HttpResponseMessage forbiddenReport = await cashier.GetAsync(
            "/api/admin/reports/financial?from=2026-10-04&to=2026-10-04"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenReport.StatusCode);
        }

        using HttpResponseMessage pendingResponse = await PostAsync(cashier,
            "/api/cashier/cash-withdrawals",
            new CreateWithdrawalRequest(10, "شراء أدوات نظافة"),
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, pendingResponse.StatusCode);
        WithdrawalResponse unresolved = await ReadAsync<WithdrawalResponse>(
            pendingResponse);
        WithdrawalResponse approvedButNotExecuted = await PostAndReadAsync<
            ReviewRequest, WithdrawalResponse>(admin,
            $"/api/admin/cashier/cash-withdrawals/{unresolved.Id}/approve",
            new ReviewRequest("موافقة قبل نهاية الشيفت",
                unresolved.RowVersion));

        _fixture.Clock.SetUtcNow(start.AddHours(4));
        ShiftResponse closingShift = await GetAsync<ShiftResponse>(cashier,
            "/api/cashier/shifts/current");
        ShiftResponse closingReconciliation = await PostAndReadAsync<
            ReconcileRequest, ShiftResponse>(cashier,
            $"/api/cashier/shifts/{shift.Id}/reconcile",
            new ReconcileRequest(60, closingShift.RowVersion));
        using (HttpResponseMessage blockedClose = await PostAsync(cashier,
            $"/api/cashier/shifts/{shift.Id}/close",
            new CloseRequest(closingReconciliation.RowVersion)))
        {
            Assert.Equal(HttpStatusCode.Conflict, blockedClose.StatusCode);
        }

        WithdrawalResponse rejected = await PostAndReadAsync<ReviewRequest,
            WithdrawalResponse>(admin,
            $"/api/admin/cashier/cash-withdrawals/{unresolved.Id}/reject",
            new ReviewRequest("لم يتم سحب المبلغ",
                approvedButNotExecuted.RowVersion));
        Assert.Equal(CashWithdrawalStatus.Rejected, rejected.Status);
        ShiftResponse closed = await PostAndReadAsync<CloseRequest, ShiftResponse>(
            cashier, $"/api/cashier/shifts/{shift.Id}/close",
            new CloseRequest(closingReconciliation.RowVersion));
        Assert.Equal(ShiftStatus.Closed, closed.Status);

        await AssertAuditAndConstraintAsync(requested.Id);
    }

    private async Task AssertAuditAndConstraintAsync(long withdrawalId)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ClinicDbContext>();
        string id = withdrawalId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        string[] actions = await dbContext.AuditLogs.AsNoTracking()
            .Where(item => item.EntityType == nameof(CashWithdrawal) &&
                item.EntityId == id)
            .Select(item => item.Action).ToArrayAsync();
        Assert.Contains("cashier.cash_withdrawal_requested", actions);
        Assert.Contains("cashier.cash_withdrawal_approved", actions);
        Assert.Contains("cashier.cash_withdrawal_executed", actions);
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [cashier].[CashWithdrawals]
                SET [Status] = 1
                WHERE [Id] = {withdrawalId};
                """));
    }

    private static async Task<SecretaryResponse> CreateSecretaryAsync(
        HttpClient admin)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        return await PostAndReadAsync<CreateSecretaryRequest, SecretaryResponse>(
            admin, "/api/admin/secretaries", new CreateSecretaryRequest(
                "سكرتيرة السحب", $"010{Random.Shared.Next(10000000, 99999999):D8}",
                null, $"withdrawal.secretary.{suffix}", "SecretaryPass1"));
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
        using HttpResponseMessage login = await PostAsync(client,
            "/api/auth/login", new LoginRequest(userName, currentPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage change = await PostAsync(client,
            "/api/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string uri)
    {
        using HttpResponseMessage response = await client.GetAsync(uri);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<T>() ??
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
        HttpClient client, string uri, TRequest request, Guid? idempotencyKey = null)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>(
            "/api/auth/csrf");
        HttpRequestMessage message = new(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        if (idempotencyKey.HasValue)
        {
            message.Headers.Add("Idempotency-Key",
                idempotencyKey.Value.ToString());
        }

        return await client.SendAsync(message);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>() ??
        throw new InvalidOperationException("Empty response.");

    private sealed record GenerateShiftsRequest(long SecretaryUserId,
        DateOnly FromDate, DateOnly ToDate,
        IReadOnlyCollection<ClinicDayOfWeek> DaysOfWeek, TimeOnly StartTime,
        TimeOnly EndTime);
    private sealed record GeneratedShiftsResponse(
        IReadOnlyCollection<ShiftResponse> Items);
    private sealed record ShiftResponse(long Id, ShiftStatus Status,
        decimal? ExpectedCash, string RowVersion);
    private sealed record OpeningBalanceRequest(decimal Amount,
        string RowVersion);
    private sealed record ReconcileRequest(decimal DeclaredCash,
        string RowVersion, string? Reason = null);
    private sealed record CloseRequest(string RowVersion,
        string? Reason = null);
    private sealed record CreateWithdrawalRequest(decimal Amount, string Reason);
    private sealed record ReviewRequest(string Reason, string RowVersion);
    private sealed record RowVersionRequest(string RowVersion);
    private sealed record WithdrawalResponse(long Id, string WithdrawalNumber,
        CashWithdrawalStatus Status, string RowVersion);
    private sealed record WithdrawalPageResponse(
        IReadOnlyCollection<WithdrawalResponse> Items);
    private sealed record ExecutedWithdrawalResponse(
        WithdrawalResponse Withdrawal, bool WasReplayed,
        decimal CurrentExpectedCash);
    private sealed record ShiftSummaryResponse(decimal CashWithdrawn,
        decimal? CurrentExpectedCash, decimal? ReconciledExpectedCash,
        int ExecutedWithdrawalCount, int PendingWithdrawalCount);
    private sealed record DashboardResponse(decimal CashWithdrawn);
    private sealed record FinancialReportResponse(decimal CashWithdrawn);
    private sealed record OperationalReportResponse(int AppointmentCount);
    private sealed record ComparisonReportResponse(
        IReadOnlyCollection<object> Points);
    private sealed record ShiftReportResponse(
        IReadOnlyCollection<ShiftReportItemResponse> Items);
    private sealed record ShiftReportItemResponse(long ShiftId,
        decimal? ExpectedCash);
    private sealed record AuditReportResponse(
        IReadOnlyCollection<AuditReportItemResponse> Items);
    private sealed record AuditReportItemResponse(string EntityId);
    private sealed record CreateSecretaryRequest(string FullName,
        string PhoneNumber, string? Email, string UserName,
        string TemporaryPassword);
    private sealed record SecretaryResponse(long Id, string UserName);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword,
        string NewPassword);
    private sealed record CsrfResponse(string Token);
}
