using Clinic.Domain.Packages;

namespace Clinic.Application.Abstractions.Packages;

public sealed record PackageServiceInput(long ServiceId, int SessionsIncluded,
    decimal UnitPriceAtDefinition);

public sealed record PackageServiceModel(long Id, long ServiceId, string ServiceName,
    long SpecializationId, string SpecializationName, int SessionsIncluded,
    decimal UnitPriceAtDefinition, bool IsActive);

public sealed record PackageModel(long Id, long DepartmentId, string DepartmentName,
    string Name, int SessionCount, decimal BasePrice, int? ActivationGraceDays,
    int? UsageDurationDays, bool IsActive, bool IsArchived,
    bool IsAvailable, string? UnavailabilityReason,
    IReadOnlyCollection<PackageServiceModel> Services, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt, string RowVersion);

public sealed record PackagePage(IReadOnlyCollection<PackageModel> Items, int PageNumber,
    int PageSize, int TotalCount);

public sealed record AdminPackageFilter(long? DepartmentId, string? Search, bool? IsActive,
    bool IncludeArchived, int PageNumber, int PageSize);

public sealed record PackageCatalogFilter(long? DepartmentId, string? Search,
    int PageNumber, int PageSize);

public sealed record PatientPackageServiceModel(long Id, long ServiceId,
    string ServiceName, long SpecializationId, string SpecializationName,
    int SessionsPurchased, decimal UnitPriceSnapshot);

public sealed record PatientPackageModel(long Id, long PatientId, long PatientFileNumber,
    string PatientName, long PackageId, long DepartmentId, string PackageName,
    string DepartmentName, int TotalSessions, decimal BasePrice, decimal NetPrice,
    int ActivationGraceDays, int UsageDurationDays, PatientPackagePaymentStatus PaymentStatus,
    PatientPackageStatus Status, bool IsUsable, string? UnavailabilityReason,
    DateTimeOffset RegisteredAt, DateTimeOffset? ActivationWindowStartedAt,
    DateTimeOffset? ActivationDeadlineAt, DateTimeOffset? FirstUsedAt,
    DateTimeOffset? ExpiresAt, int AvailableSessions, int ReservedSessions,
    int ConsumedSessions, IReadOnlyCollection<PatientPackageServiceModel> Services,
    string RowVersion);

public sealed record PackageSessionModel(long Id, long PatientPackageServiceId, long ServiceId,
    string ServiceName, int SequenceNumber, decimal UnitPriceSnapshot,
    PackageSessionStatus Status, DateTimeOffset? ReservedAt, DateTimeOffset? ConsumedAt,
    string RowVersion);

public sealed record PatientPackageRegistrationResult(PatientPackageModel PatientPackage,
    bool IsReplay);

public sealed record PatientPackagePage(IReadOnlyCollection<PatientPackageModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public sealed record AdminPatientPackageFilter(long? PatientId, long? DepartmentId,
    PatientPackagePaymentStatus? PaymentStatus, PatientPackageStatus? Status, bool? IsUsable,
    string? Search, int PageNumber, int PageSize);
