namespace Clinic.Application.Features.Identity.Secretaries;

public sealed class ListSecretariesQueryHandler
    : IQueryHandler<ListSecretariesQuery, PagedResult<SecretarySummary>>
{
    private const int MaximumPageSize = 100;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretaryAccountService _secretaryAccountService;

    public ListSecretariesQueryHandler(
        ICurrentUser currentUser,
        ISecretaryAccountService secretaryAccountService)
    {
        _currentUser = currentUser;
        _secretaryAccountService = secretaryAccountService;
    }

    public Task<Result<PagedResult<SecretarySummary>>> Handle(
        ListSecretariesQuery query,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long adminUserId)
        {
            return Task.FromResult(Result.Failure<PagedResult<SecretarySummary>>(
                IdentityErrors.NotAuthenticated));
        }

        if (query.PageNumber < 1 || query.PageSize is < 1 or > MaximumPageSize)
        {
            return Task.FromResult(Result.Failure<PagedResult<SecretarySummary>>(
                IdentityErrors.Validation("بيانات الصفحة غير صحيحة.")));
        }

        return _secretaryAccountService.ListAsync(
            adminUserId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
    }
}
