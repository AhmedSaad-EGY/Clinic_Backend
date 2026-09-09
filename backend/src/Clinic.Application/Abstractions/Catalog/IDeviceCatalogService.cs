using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Catalog;

public interface IDeviceCatalogService
{
    Task<Result<DeviceModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        string name,
        string? identifier,
        CancellationToken cancellationToken);

    Task<Result<DeviceModel>> UpdateAsync(
        long actorUserId,
        long deviceId,
        string name,
        string? identifier,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result> ArchiveAsync(
        long actorUserId,
        long deviceId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
