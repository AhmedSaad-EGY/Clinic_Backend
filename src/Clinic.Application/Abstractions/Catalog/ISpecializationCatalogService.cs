namespace Clinic.Application.Abstractions.Catalog;

public interface ISpecializationCatalogService
{
    Task<Result<SpecializationModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        string name,
        CancellationToken cancellationToken);

    Task<Result<SpecializationModel>> UpdateAsync(
        long actorUserId,
        long specializationId,
        string name,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result> ArchiveAsync(
        long actorUserId,
        long specializationId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
