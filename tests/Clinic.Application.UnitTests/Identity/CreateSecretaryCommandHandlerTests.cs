using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Identity.Secretaries;

namespace Clinic.Application.UnitTests.Identity;

public sealed class CreateSecretaryCommandHandlerTests
{
    [Fact]
    public async Task HandleRejectsUnauthenticatedCaller()
    {
        SecretaryAccountServiceStub service = new();
        CreateSecretaryCommandHandler handler = new(
            new CurrentUserStub(userId: null),
            service);

        Result<SecretarySummary> result = await handler.Handle(
            new CreateSecretaryCommand(
                "سارة محمد",
                "01000000000",
                null,
                "sara",
                "SafePassword1"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.NotAuthenticated, result.Error);
        Assert.False(service.WasCreateCalled);
    }

    [Fact]
    public async Task HandleTrimsSecretaryDataAndUsesCurrentAdmin()
    {
        SecretarySummary secretary = new(
            9,
            "سارة محمد",
            "sara",
            "01000000000",
            null,
            false,
            false,
            DateTimeOffset.UtcNow);
        SecretaryAccountServiceStub service = new(Result.Success(secretary));
        CreateSecretaryCommandHandler handler = new(new CurrentUserStub(3), service);

        Result<SecretarySummary> result = await handler.Handle(
            new CreateSecretaryCommand(
                "  سارة محمد  ",
                " 01000000000 ",
                null,
                " sara ",
                "SafePassword1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, service.ReceivedAdminUserId);
        Assert.Equal("سارة محمد", service.ReceivedFullName);
        Assert.Equal("01000000000", service.ReceivedPhoneNumber);
        Assert.Equal("sara", service.ReceivedUserName);
    }

    private sealed class CurrentUserStub : ICurrentUser
    {
        public CurrentUserStub(long? userId)
        {
            UserId = userId;
        }

        public long? UserId { get; }

        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class SecretaryAccountServiceStub : ISecretaryAccountService
    {
        private readonly Result<SecretarySummary> _createResult;

        public SecretaryAccountServiceStub(Result<SecretarySummary>? createResult = null)
        {
            _createResult = createResult ??
                Result.Failure<SecretarySummary>(IdentityErrors.OperationFailed("not configured"));
        }

        public bool WasCreateCalled { get; private set; }

        public long ReceivedAdminUserId { get; private set; }

        public string? ReceivedFullName { get; private set; }

        public string? ReceivedPhoneNumber { get; private set; }

        public string? ReceivedUserName { get; private set; }

        public Task<Result<SecretarySummary>> CreateAsync(
            long adminUserId,
            string fullName,
            string phoneNumber,
            string? email,
            string userName,
            string temporaryPassword,
            CancellationToken cancellationToken)
        {
            WasCreateCalled = true;
            ReceivedAdminUserId = adminUserId;
            ReceivedFullName = fullName;
            ReceivedPhoneNumber = phoneNumber;
            ReceivedUserName = userName;
            return Task.FromResult(_createResult);
        }

        public Task<Result<PagedResult<SecretarySummary>>> ListAsync(
            long adminUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> DisableAsync(
            long adminUserId,
            long secretaryUserId,
            string reason,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> EnableAsync(
            long adminUserId,
            long secretaryUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> UnlockAsync(
            long adminUserId,
            long secretaryUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> ResetPasswordAsync(
            long adminUserId,
            long secretaryUserId,
            string temporaryPassword,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
