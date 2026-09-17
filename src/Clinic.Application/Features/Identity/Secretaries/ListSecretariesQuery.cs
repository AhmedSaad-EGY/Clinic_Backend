namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record ListSecretariesQuery(
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResult<SecretarySummary>>;
