namespace Clinic.Application.Abstractions.Catalog;

public interface IDepartmentCatalogService
{
    Task<Result<DepartmentModel>> CreateAsync(
        long actorUserId,
        string name,
        string? description,
        string roomName,
        CancellationToken cancellationToken);

    Task<Result<DepartmentModel>> UpdateAsync(
        long actorUserId,
        long departmentId,
        string name,
        string? description,
        string roomName,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result> ArchiveAsync(
        long actorUserId,
        long departmentId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
