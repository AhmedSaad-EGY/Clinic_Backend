using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Identity.Authentication;

namespace Clinic.Application.UnitTests.Identity;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task HandleRejectsMissingCredentialsWithoutCallingIdentityStore()
    {
        AuthenticationServiceStub service = new();
        LoginCommandHandler handler = new(service);

        Result<UserProfile> result = await handler.Handle(
            new LoginCommand(" ", "password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.validation", result.Error.Code);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task HandleTrimsUserNameAndDelegatesAuthentication()
    {
        UserProfile profile = new(7, "سارة محمد", "sara", RoleNames.Secretary, true);
        AuthenticationServiceStub service = new(Result.Success(profile));
        LoginCommandHandler handler = new(service);

        Result<UserProfile> result = await handler.Handle(
            new LoginCommand("  sara  ", "SafePassword1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(profile, result.Value);
        Assert.Equal("sara", service.ReceivedUserName);
        Assert.Equal("SafePassword1", service.ReceivedPassword);
    }

    private sealed class AuthenticationServiceStub : IAuthenticationService
    {
        private readonly Result<UserProfile> _result;

        public AuthenticationServiceStub(Result<UserProfile>? result = null)
        {
            _result = result ?? Result.Failure<UserProfile>(IdentityErrors.InvalidCredentials);
        }

        public bool WasCalled { get; private set; }

        public string? ReceivedUserName { get; private set; }

        public string? ReceivedPassword { get; private set; }

        public Task<Result<UserProfile>> SignInAsync(
            string userName,
            string password,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedUserName = userName;
            ReceivedPassword = password;
            return Task.FromResult(_result);
        }

        public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Result<UserProfile>> GetUserAsync(
            long userId,
            CancellationToken cancellationToken) => Task.FromResult(_result);

        public Task<Result<UserProfile>> ChangePasswordAsync(
            long userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken) => Task.FromResult(_result);
    }
}
