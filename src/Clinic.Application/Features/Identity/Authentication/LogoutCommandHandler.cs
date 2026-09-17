namespace Clinic.Application.Features.Identity.Authentication;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IAuthenticationService _authenticationService;

    public LogoutCommandHandler(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public async Task<Result> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        await _authenticationService.SignOutAsync(cancellationToken);
        return Result.Success();
    }
}
