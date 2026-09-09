using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed class UnlockSecretaryCommandHandler : ICommandHandler<UnlockSecretaryCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISecretaryAccountService _secretaryAccountService;

    public UnlockSecretaryCommandHandler(
        ICurrentUser currentUser,
        ISecretaryAccountService secretaryAccountService)
    {
        _currentUser = currentUser;
        _secretaryAccountService = secretaryAccountService;
    }

    public Task<Result> Handle(
        UnlockSecretaryCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long adminUserId)
        {
            return Task.FromResult(Result.Failure(IdentityErrors.NotAuthenticated));
        }

        return command.SecretaryUserId <= 0
            ? Task.FromResult(Result.Failure(
                IdentityErrors.Validation("الحساب المطلوب غير صحيح.")))
            : _secretaryAccountService.UnlockAsync(
                adminUserId,
                command.SecretaryUserId,
                cancellationToken);
    }
}
