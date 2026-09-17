namespace Clinic.Application.Abstractions.Scheduling;

public sealed record DoctorModel(
    long Id,
    long DepartmentId,
    string Name,
    string? Phone,
    bool IsActive,
    bool IsArchived,
    IReadOnlyCollection<DoctorServiceModel> Services,
    string RowVersion);

public sealed record DoctorServiceModel(long Id, long ServiceId, string ServiceName, bool IsActive);

public sealed record DoctorScheduleModel(
    long Id,
    long DoctorId,
    ClinicDayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    string RowVersion);

public sealed record DoctorExceptionModel(
    long Id,
    long DoctorId,
    DateOnly ExceptionDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    DoctorExceptionType Type,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string RowVersion);

public sealed record DepartmentClosureModel(
    long Id,
    long DepartmentId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Reason,
    DateTimeOffset? CancelledAt,
    string RowVersion);

public sealed record DepartmentAvailabilityModel(
    long DepartmentId,
    DepartmentStatus Status,
    DepartmentClosureModel? CurrentClosure,
    IReadOnlyCollection<DepartmentClosureModel> UpcomingClosures,
    bool HasMoreUpcomingClosures);

public sealed record SchedulingPage<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
