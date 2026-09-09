using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Identity;

public sealed class IdentityFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public IdentityFlowTests(IdentitySqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IdentityLifecycleEnforcesPasswordChangeRevocationLockoutAndAudit()
    {
        using HttpClient adminClient = _fixture.CreateClient();
        string adminCsrf = await GetCsrfTokenAsync(adminClient);

        using HttpResponseMessage initialLogin = await PostAsync(
            adminClient,
            "/api/auth/login",
            new LoginRequest("integration.admin", IdentitySqlServerFixture.InitialAdminPassword),
            adminCsrf);
        UserProfileResponse? initialAdmin =
            await initialLogin.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, initialLogin.StatusCode);
        Assert.True(initialAdmin?.MustChangePassword);

        adminCsrf = await GetCsrfTokenAsync(adminClient);
        using HttpResponseMessage passwordChange = await PostAsync(
            adminClient,
            "/api/auth/change-password",
            new ChangePasswordRequest(
                IdentitySqlServerFixture.InitialAdminPassword,
                IdentitySqlServerFixture.UpdatedAdminPassword),
            adminCsrf);
        UserProfileResponse? updatedAdmin =
            await passwordChange.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, passwordChange.StatusCode);
        Assert.False(updatedAdmin?.MustChangePassword);

        const string disabledUserName = "disabled.secretary";
        const string disabledPassword = "SecretaryPass1";
        await CreateSecretaryAsync(
            adminClient,
            disabledUserName,
            "01011111111",
            disabledPassword);

        using HttpClient secretaryClient = _fixture.CreateClient();
        string secretaryCsrf = await GetCsrfTokenAsync(secretaryClient);
        using HttpResponseMessage secretaryLogin = await PostAsync(
            secretaryClient,
            "/api/auth/login",
            new LoginRequest(disabledUserName, disabledPassword),
            secretaryCsrf);
        Assert.Equal(HttpStatusCode.OK, secretaryLogin.StatusCode);

        adminCsrf = await GetCsrfTokenAsync(adminClient);
        long disabledSecretaryId = await FindUserIdAsync(disabledUserName);
        using HttpResponseMessage disableResponse = await PostAsync(
            adminClient,
            $"/api/admin/secretaries/{disabledSecretaryId}/disable",
            new DisableSecretaryRequest("اختبار إبطال الجلسة"),
            adminCsrf);
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        using HttpResponseMessage rejectedSession = await secretaryClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedSession.StatusCode);

        const string lockedUserName = "locked.secretary";
        const string lockedPassword = "SecretaryPass2";
        await CreateSecretaryAsync(
            adminClient,
            lockedUserName,
            "01022222222",
            lockedPassword);

        using HttpClient loginClient = _fixture.CreateClient();
        string loginCsrf = await GetCsrfTokenAsync(loginClient);

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using HttpResponseMessage failedLogin = await PostAsync(
                loginClient,
                "/api/auth/login",
                new LoginRequest(lockedUserName, "WrongPassword1"),
                loginCsrf);
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        using HttpResponseMessage lockoutLogin = await PostAsync(
            loginClient,
            "/api/auth/login",
            new LoginRequest(lockedUserName, "WrongPassword1"),
            loginCsrf);
        Assert.Equal((HttpStatusCode)423, lockoutLogin.StatusCode);

        using HttpResponseMessage rejectedCorrectPassword = await PostAsync(
            loginClient,
            "/api/auth/login",
            new LoginRequest(lockedUserName, lockedPassword),
            loginCsrf);
        Assert.Equal((HttpStatusCode)423, rejectedCorrectPassword.StatusCode);

        await AssertLockoutAuditAsync(lockedUserName);
    }

    private static async Task CreateSecretaryAsync(
        HttpClient adminClient,
        string userName,
        string phoneNumber,
        string temporaryPassword)
    {
        string csrfToken = await GetCsrfTokenAsync(adminClient);
        using HttpResponseMessage response = await PostAsync(
            adminClient,
            "/api/admin/secretaries",
            new CreateSecretaryRequest(
                "سكرتيرة اختبار",
                phoneNumber,
                null,
                userName,
                temporaryPassword),
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<long> FindUserIdAsync(string userName)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        string normalizedUserName = userName.ToUpperInvariant();

        return await dbContext.Users
            .Where(user => user.NormalizedUserName == normalizedUserName)
            .Select(user => user.Id)
            .SingleAsync();
    }

    private async Task AssertLockoutAuditAsync(string userName)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        string normalizedUserName = userName.ToUpperInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.NormalizedUserName == normalizedUserName);
        string userId = user.Id.ToString(CultureInfo.InvariantCulture);
        List<string> actions = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog => auditLog.EntityId == userId)
            .Select(auditLog => auditLog.Action)
            .ToListAsync();

        Assert.Equal(4, actions.Count(action => action == "identity.login_failed"));
        Assert.Contains("identity.account_locked", actions);
        Assert.Contains("identity.login_rejected_locked", actions);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        CsrfResponse? payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();

        return payload?.Token ?? throw new InvalidOperationException(
            "The API did not return an antiforgery token.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(
        HttpClient client,
        string requestUri,
        TRequest request,
        string csrfToken)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrfToken);

        return await client.SendAsync(message);
    }

    private sealed record CsrfResponse(string Token);

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record CreateSecretaryRequest(
        string FullName,
        string PhoneNumber,
        string? Email,
        string UserName,
        string TemporaryPassword);

    private sealed record DisableSecretaryRequest(string Reason);

    private sealed record UserProfileResponse(
        long Id,
        string FullName,
        string UserName,
        string Role,
        bool MustChangePassword);
}
