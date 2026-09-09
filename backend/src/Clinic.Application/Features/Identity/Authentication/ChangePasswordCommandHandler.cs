using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Authentication;

public sealed class ChangePasswordCommandHandler
    : ICommandHandler<ChangePasswordCommand, UserProfile>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthenticationService _authenticationService;

    public ChangePasswordCommandHandler(
        ICurrentUser currentUser,
        IAuthenticationService authenticationService)
    {
        _currentUser = currentUser;
        _authenticationService = authenticationService;
    }

    public Task<Result<UserProfile>> Handle(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long userId)
        {
            return Task.FromResult(Result.Failure<UserProfile>(IdentityErrors.NotAuthenticated));
        }

        if (string.IsNullOrWhiteSpace(command.CurrentPassword) ||
            string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return Task.FromResult(Result.Failure<UserProfile>(
                IdentityErrors.Validation("كلمة المرور الحالية والجديدة مطلوبتان.")));
        }

        if (command.CurrentPassword.Length > 128 || command.NewPassword.Length > 128)
        {
            return Task.FromResult(Result.Failure<UserProfile>(
                IdentityErrors.Validation("كلمة المرور تتجاوز الطول المسموح.")));
        }

        return _authenticationService.ChangePasswordAsync(
            userId,
            command.CurrentPassword,
            command.NewPassword,
            cancellationToken);
    }
}
