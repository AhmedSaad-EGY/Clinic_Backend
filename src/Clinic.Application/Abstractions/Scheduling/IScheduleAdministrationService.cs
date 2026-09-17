namespace Clinic.Application.Abstractions.Scheduling;

public interface IScheduleAdministrationService
{
    Task<Result<DoctorScheduleModel>> CreateScheduleAsync(long actorUserId, long doctorId, ClinicDayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken cancellationToken);
    Task<Result<DoctorScheduleModel>> UpdateScheduleAsync(long actorUserId, long scheduleId, ClinicDayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime, DateOnly effectiveFrom, DateOnly? effectiveTo, byte[] rowVersion, bool confirmAffectedAppointments, CancellationToken cancellationToken);
    Task<Result> DeactivateScheduleAsync(long actorUserId, long scheduleId, byte[] rowVersion, bool confirmAffectedAppointments, CancellationToken cancellationToken);
    Task<Result<DoctorExceptionModel>> CreateExceptionAsync(long actorUserId, long doctorId, DateOnly exceptionDate, TimeOnly? startTime, TimeOnly? endTime, DoctorExceptionType type, string? reason, bool confirmAffectedAppointments, CancellationToken cancellationToken);
    Task<Result> CancelExceptionAsync(long actorUserId, long exceptionId, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<DepartmentClosureModel>> CreateClosureAsync(long actorUserId, long departmentId, DateTimeOffset startAt, DateTimeOffset endAt, string reason, bool confirmAffectedAppointments, CancellationToken cancellationToken);
    Task<Result> CancelClosureAsync(long actorUserId, long closureId, byte[] rowVersion, CancellationToken cancellationToken);
}
