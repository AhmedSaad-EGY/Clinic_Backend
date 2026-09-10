using Clinic.Domain.Discounts;

namespace Clinic.Api.Contracts.Discounts;

public sealed record DiscountTargetsRequest(IReadOnlyCollection<long>? DepartmentIds,
    IReadOnlyCollection<long>? ServiceIds, IReadOnlyCollection<long>? PackageIds);

public sealed record CreateDiscountRequest(string Name, DiscountType Type, decimal Value,
    DiscountAppliesTo AppliesTo, DiscountScopeMode ScopeMode,
    DateTimeOffset StartAt, DateTimeOffset EndAt, DiscountTargetsRequest Targets);

public sealed record UpdateDiscountRequest(string Name, DiscountType Type, decimal Value,
    DiscountAppliesTo AppliesTo, DiscountScopeMode ScopeMode,
    DateTimeOffset StartAt, DateTimeOffset EndAt, DiscountTargetsRequest Targets,
    string RowVersion);

public sealed record SetDiscountActivationRequest(bool IsActive, string RowVersion);
public sealed record ArchiveDiscountRequest(string RowVersion);
