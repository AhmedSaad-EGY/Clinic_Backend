using Clinic.Domain.Packages;

namespace Clinic.Api.Contracts.Packages;

public sealed record PackageServiceRequest(long ServiceId, int SessionsIncluded,
    decimal UnitPriceAtDefinition);

public sealed record CreatePackageRequest(long DepartmentId, string Name, decimal BasePrice,
    int ActivationGraceDays, int UsageDurationDays,
    IReadOnlyCollection<PackageServiceRequest> Services);

public sealed record UpdatePackageRequest(string Name, decimal BasePrice,
    int ActivationGraceDays, int UsageDurationDays,
    IReadOnlyCollection<PackageServiceRequest> Services, string RowVersion);

public sealed record SetPackageActivationRequest(bool IsActive, string RowVersion);

public sealed record ArchivePackageRequest(string RowVersion);

public sealed record RegisterPatientPackageRequest(long PackageId, string PackageRowVersion);

public sealed record ExtendPatientPackageRequest(PatientPackageExtensionType ExtensionType,
    DateTimeOffset NewDeadline, string Reason, string RowVersion);
