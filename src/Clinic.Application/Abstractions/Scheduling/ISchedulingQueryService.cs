namespace Clinic.Application.Abstractions.Scheduling;

public interface ISchedulingQueryService
{
    Task<Result<SchedulingPage<DoctorModel>>> ListDoctorsAsync(long? departmentId, long? serviceId, bool includeArchived, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<DoctorModel>> GetDoctorAsync(long doctorId, bool includeArchived, CancellationToken cancellationToken);
    Task<Result<SchedulingPage<DoctorScheduleModel>>> ListSchedulesAsync(long doctorId, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<SchedulingPage<DoctorExceptionModel>>> ListExceptionsAsync(long doctorId, DateOnly fromDate, DateOnly toDate, bool includeCancelled, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<DepartmentAvailabilityModel>> GetDepartmentAvailabilityAsync(long departmentId, DateTimeOffset at, CancellationToken cancellationToken);
    Task<Result<SchedulingPage<DepartmentClosureModel>>> ListClosuresAsync(long departmentId, DateTimeOffset? fromDate, DateTimeOffset? toDate, bool includeCancelled, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
