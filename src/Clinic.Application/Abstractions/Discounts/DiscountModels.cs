namespace Clinic.Application.Abstractions.Discounts;

public sealed record DiscountTargetInput(
    IReadOnlyCollection<long> DepartmentIds,
    IReadOnlyCollection<long> ServiceIds,
    IReadOnlyCollection<long> PackageIds);

public sealed record DiscountTargetModel(long Id, string Name);

public sealed record DiscountModel(long Id, string Name, DiscountType Type, decimal Value,
    DiscountAppliesTo AppliesTo, DiscountScopeMode ScopeMode,
    DateTimeOffset StartAt, DateTimeOffset EndAt, bool IsActive, bool IsArchived,
    bool IsCurrentlyEffective, IReadOnlyCollection<DiscountTargetModel> Departments,
    IReadOnlyCollection<DiscountTargetModel> Services,
    IReadOnlyCollection<DiscountTargetModel> Packages, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt, string RowVersion);

public sealed record DiscountPage(IReadOnlyCollection<DiscountModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public sealed record DiscountFilter(string? Search, DiscountType? Type,
    DiscountAppliesTo? AppliesTo, long? DepartmentId, bool? IsActive,
    DateTimeOffset? EffectiveAt, bool IncludeArchived, int PageNumber, int PageSize);

public sealed record DiscountDefinition(string Name, DiscountType Type, decimal Value,
    DiscountAppliesTo AppliesTo, DiscountScopeMode ScopeMode,
    DateTimeOffset StartAt, DateTimeOffset EndAt, DiscountTargetInput Targets);
