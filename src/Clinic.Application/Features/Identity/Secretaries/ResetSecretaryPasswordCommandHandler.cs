using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed class ResetSecretaryPasswordCommandHandler
    : ICommandHandler<ResetSecretaryPasswordCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISecretaryAccountService _secretaryAccountService;

    public ResetSecretaryPasswordCommandHandler(
        ICurrentUser currentUser,
        ISecretaryAccountService secretaryAccountService)
    {
        _currentUser = currentUser;
        _secretaryAccountService = secretaryAccountService;
    }

    public Task<Result> Handle(
        ResetSecretaryPasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long adminUserId)
        {
            return Task.FromResult(Result.Failure(IdentityErrors.NotAuthenticated));
        }

        if (command.SecretaryUserId <= 0 ||
            string.IsNullOrWhiteSpace(command.TemporaryPassword) ||
            command.TemporaryPassword.Length > 128)
        {
            return Task.FromResult(Result.Failure(
                IdentityErrors.Validation("كلمة المرور المؤقتة مطلوبة.")));
        }

        return _secretaryAccountService.ResetPasswordAsync(
            adminUserId,
            command.SecretaryUserId,
            command.TemporaryPassword,
            cancellationToken);
    }
}
