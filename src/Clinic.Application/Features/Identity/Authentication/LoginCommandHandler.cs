namespace Clinic.Application.Features.Identity.Authentication;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, UserProfile>
{
    private readonly IAuthenticationService _authenticationService;

    public LoginCommandHandler(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public Task<Result<UserProfile>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.UserName) ||
            string.IsNullOrWhiteSpace(command.Password))
        {
            return Task.FromResult(Result.Failure<UserProfile>(
                IdentityErrors.Validation("اسم المستخدم وكلمة المرور مطلوبان.")));
        }

        if (command.UserName.Trim().Length > 256 || command.Password.Length > 128)
        {
            return Task.FromResult(Result.Failure<UserProfile>(
                IdentityErrors.InvalidCredentials));
        }

        return _authenticationService.SignInAsync(
            command.UserName.Trim(),
            command.Password,
            cancellationToken);
    }
}
