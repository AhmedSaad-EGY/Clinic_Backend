namespace Clinic.Application.Abstractions.Identity;

public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
