namespace Clinic.Api.Contracts.Scheduling;

public sealed record CreateDoctorRequest(long DepartmentId, string Name, string? Phone);
public sealed record UpdateDoctorRequest(string Name, string? Phone, bool IsActive, string RowVersion);
public sealed record ReplaceDoctorServicesRequest(IReadOnlyCollection<long> ServiceIds, string RowVersion);
public sealed record SchedulingRowVersionRequest(string RowVersion, bool ConfirmAffectedAppointments = false);
public sealed record CreateDoctorScheduleRequest(ClinicDayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record UpdateDoctorScheduleRequest(ClinicDayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string RowVersion, bool ConfirmAffectedAppointments = false);
public sealed record CreateDoctorExceptionRequest(DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, DoctorExceptionType Type, string? Reason, bool ConfirmAffectedAppointments = false);
public sealed record CreateDepartmentClosureRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason, bool ConfirmAffectedAppointments = false);
