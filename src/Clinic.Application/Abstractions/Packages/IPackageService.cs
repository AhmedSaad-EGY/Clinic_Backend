using Clinic.Application.Common;
using Clinic.Domain.Packages;

namespace Clinic.Application.Abstractions.Packages;

public interface IPackageCommandService
{
    Task<Result<PackageModel>> CreateAsync(long actorUserId, long departmentId, string name,
        decimal basePrice, int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<PackageServiceInput> services,
        CancellationToken cancellationToken);

    Task<Result<PackageModel>> UpdateAsync(long actorUserId, long packageId, string name,
        decimal basePrice, int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<PackageServiceInput> services,
        byte[] expectedRowVersion, CancellationToken cancellationToken);

    Task<Result<PackageModel>> SetActiveAsync(long actorUserId, long packageId, bool isActive,
        byte[] expectedRowVersion, CancellationToken cancellationToken);

    Task<Result> ArchiveAsync(long actorUserId, long packageId, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}

public interface IPatientPackageCommandService
{
    Task<Result<PatientPackageRegistrationResult>> RegisterAsync(long actorUserId,
        long patientId, long packageId, byte[] expectedPackageRowVersion, Guid idempotencyKey,
        CancellationToken cancellationToken);
    Task<Result<PatientPackageModel>> ExtendAsync(long adminUserId, long patientPackageId,
        PatientPackageExtensionType extensionType, DateTimeOffset newDeadline, string reason,
        byte[] expectedRowVersion, CancellationToken cancellationToken);
}

public interface IPatientPackageQueryService
{
    Task<Result<PatientPackagePage>> ListForPatientAsync(long patientId, int pageNumber,
        int pageSize, CancellationToken cancellationToken);
    Task<Result<PatientPackageModel>> GetAsync(long patientPackageId,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<PackageSessionModel>>> ListSessionsAsync(long patientPackageId,
        CancellationToken cancellationToken);
    Task<Result<PatientPackagePage>> SearchAdminAsync(AdminPatientPackageFilter filter,
        CancellationToken cancellationToken);
}

public interface IPackageQueryService
{
    Task<Result<PackagePage>> SearchAdminAsync(AdminPackageFilter filter,
        CancellationToken cancellationToken);
    Task<Result<PackageModel>> GetAdminAsync(long packageId,
        CancellationToken cancellationToken);
    Task<Result<PackagePage>> SearchAvailableAsync(PackageCatalogFilter filter,
        CancellationToken cancellationToken);
    Task<Result<PackageModel>> GetAvailableAsync(long packageId,
        CancellationToken cancellationToken);
}
