namespace Clinic.Application.Abstractions.Scheduling;

public interface IDoctorAdministrationService
{
    Task<Result<DoctorModel>> CreateAsync(long actorUserId, long departmentId, string name, string? phone, CancellationToken cancellationToken);
    Task<Result<DoctorModel>> UpdateAsync(long actorUserId, long doctorId, string name, string? phone, bool isActive, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<DoctorModel>> ReplaceServicesAsync(long actorUserId, long doctorId, IReadOnlyCollection<long> serviceIds, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result> ArchiveAsync(long actorUserId, long doctorId, byte[] rowVersion, CancellationToken cancellationToken);
}
