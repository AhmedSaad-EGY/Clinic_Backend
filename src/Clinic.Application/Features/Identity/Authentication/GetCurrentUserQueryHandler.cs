namespace Clinic.Application.Features.Identity.Authentication;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, UserProfile>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthenticationService _authenticationService;

    public GetCurrentUserQueryHandler(
        ICurrentUser currentUser,
        IAuthenticationService authenticationService)
    {
        _currentUser = currentUser;
        _authenticationService = authenticationService;
    }

    public Task<Result<UserProfile>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken) =>
        _currentUser.UserId is long userId
            ? _authenticationService.GetUserAsync(userId, cancellationToken)
            : Task.FromResult(Result.Failure<UserProfile>(IdentityErrors.NotAuthenticated));
}
