namespace Clinic.Infrastructure.Scheduling;

internal static class SchedulingInfrastructureSupport
{
    public static bool MatchesVersion(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    public static void AddAudit(
        ClinicDbContext dbContext,
        long actorUserId,
        string action,
        string entityType,
        long entityId,
        DateTimeOffset occurredAt) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(
            actorUserId,
            action,
            entityType,
            entityId.ToString(CultureInfo.InvariantCulture),
            occurredAt));

    public static ResultError MapDatabaseFailure(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
            ? SchedulingErrors.ConcurrencyConflict
            : throw new InvalidOperationException("Scheduling persistence failed.", exception);
}

internal static class SchedulingMapper
{
    public static DoctorModel Map(
        Doctor doctor,
        IEnumerable<DoctorServiceModel>? services = null) => new(
        doctor.Id,
        doctor.DepartmentId,
        doctor.Name,
        doctor.Phone,
        doctor.IsActive,
        doctor.IsArchived,
        services?.ToArray() ?? [],
        Convert.ToBase64String(doctor.RowVersion));

    public static DoctorScheduleModel Map(DoctorSchedule schedule) => new(
        schedule.Id,
        schedule.DoctorId,
        schedule.DayOfWeek,
        schedule.StartTime,
        schedule.EndTime,
        schedule.EffectiveFrom,
        schedule.EffectiveTo,
        schedule.IsActive,
        Convert.ToBase64String(schedule.RowVersion));

    public static DoctorExceptionModel Map(DoctorScheduleOverride item) => new(
        item.Id,
        item.DoctorId,
        item.ExceptionDate,
        item.StartTime,
        item.EndTime,
        item.Type,
        item.Reason,
        item.CreatedAt,
        item.CancelledAt,
        Convert.ToBase64String(item.RowVersion));

    public static DepartmentClosureModel Map(DepartmentClosure item) => new(
        item.Id,
        item.DepartmentId,
        item.StartAt,
        item.EndAt,
        item.Reason,
        item.CancelledAt,
        Convert.ToBase64String(item.RowVersion));
}

internal static class SchedulingAuditActions
{
    public const string DoctorCreated = "scheduling.doctor.created";
    public const string DoctorUpdated = "scheduling.doctor.updated";
    public const string DoctorArchived = "scheduling.doctor.archived";
    public const string DoctorServicesReplaced = "scheduling.doctor.services_replaced";
    public const string ScheduleCreated = "scheduling.schedule.created";
    public const string ScheduleUpdated = "scheduling.schedule.updated";
    public const string ScheduleDeactivated = "scheduling.schedule.deactivated";
    public const string ExceptionCreated = "scheduling.exception.created";
    public const string ExceptionCancelled = "scheduling.exception.cancelled";
    public const string ClosureCreated = "scheduling.closure.created";
    public const string ClosureCancelled = "scheduling.closure.cancelled";
}
