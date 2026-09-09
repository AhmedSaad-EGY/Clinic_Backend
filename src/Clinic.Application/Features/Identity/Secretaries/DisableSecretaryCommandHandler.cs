using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed class DisableSecretaryCommandHandler : ICommandHandler<DisableSecretaryCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISecretaryAccountService _secretaryAccountService;

    public DisableSecretaryCommandHandler(
        ICurrentUser currentUser,
        ISecretaryAccountService secretaryAccountService)
    {
        _currentUser = currentUser;
        _secretaryAccountService = secretaryAccountService;
    }

    public Task<Result> Handle(
        DisableSecretaryCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long adminUserId)
        {
            return Task.FromResult(Result.Failure(IdentityErrors.NotAuthenticated));
        }

        if (command.SecretaryUserId <= 0 ||
            string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Trim().Length > 500)
        {
            return Task.FromResult(Result.Failure(
                IdentityErrors.Validation("الحساب وسبب التعطيل مطلوبان.")));
        }

        return _secretaryAccountService.DisableAsync(
            adminUserId,
            command.SecretaryUserId,
            command.Reason.Trim(),
            cancellationToken);
    }
}
