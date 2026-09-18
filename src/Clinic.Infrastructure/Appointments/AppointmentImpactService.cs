namespace Clinic.Infrastructure.Appointments;

public sealed class AppointmentImpactService(ClinicDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<Result> ApplyDepartmentClosureAsync(long actorUserId, long departmentId,
        DateTimeOffset startAt, DateTimeOffset endAt, bool confirmed,
        CancellationToken cancellationToken)
    {
        Appointment[] affected = await dbContext.Appointments.Where(item =>
            item.DepartmentId == departmentId &&
            (item.Status == AppointmentStatus.Booked || item.Status == AppointmentStatus.Confirmed) &&
            item.StartAt < endAt && startAt < item.EndAt).ToArrayAsync(cancellationToken);
        return Apply(affected, actorUserId, confirmed);
    }

    public async Task<Result> ApplyDoctorExceptionAsync(long actorUserId, long doctorId,
        DateOnly exceptionDate, TimeOnly? startTime, TimeOnly? endTime, bool confirmed,
        CancellationToken cancellationToken)
    {
        DateTimeOffset dayStart = AppointmentInfrastructureSupport.ClinicDayStartUtc(exceptionDate);
        DateTimeOffset dayEnd = AppointmentInfrastructureSupport.ClinicDayStartUtc(
            exceptionDate.AddDays(1));
        Appointment[] candidates = await dbContext.Appointments
            .Include(item => item.Services).ThenInclude(item => item.DoctorService)
            .Where(item => (item.Status == AppointmentStatus.Booked ||
                item.Status == AppointmentStatus.Confirmed) &&
                item.StartAt < dayEnd && dayStart < item.EndAt &&
                item.Services.Any(line => line.Status == AppointmentServiceStatus.Scheduled &&
                    line.DoctorService.DoctorId == doctorId))
            .ToArrayAsync(cancellationToken);
        Appointment[] affected = candidates.Where(appointment => appointment.Services.Any(line =>
        {
            if (line.Status != AppointmentServiceStatus.Scheduled ||
                line.DoctorService.DoctorId != doctorId) return false;
            DateTimeOffset localStart = TimeZoneInfo.ConvertTime(line.SegmentStartAt,
                AppointmentInfrastructureSupport.ClinicTimeZone);
            DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(line.SegmentEndAt,
                AppointmentInfrastructureSupport.ClinicTimeZone);
            if (DateOnly.FromDateTime(localStart.DateTime) != exceptionDate) return false;
            return !startTime.HasValue ||
                startTime < TimeOnly.FromDateTime(localEnd.DateTime) &&
                TimeOnly.FromDateTime(localStart.DateTime) < endTime;
        })).ToArray();
        return Apply(affected, actorUserId, confirmed);
    }

    public async Task<Result> ApplyDoctorScheduleChangeAsync(long actorUserId, long doctorId,
        bool confirmed, CancellationToken cancellationToken)
    {
        DoctorSchedule[] schedules = (await dbContext.DoctorSchedules
            .Where(item => item.DoctorId == doctorId)
            .ToArrayAsync(cancellationToken)).Where(item => item.IsActive).ToArray();
        DoctorScheduleOverride[] exceptions = await dbContext.DoctorExceptions
            .Where(item => item.DoctorId == doctorId && item.CancelledAt == null)
            .ToArrayAsync(cancellationToken);
        Appointment[] candidates = await dbContext.Appointments
            .Include(item => item.Services).ThenInclude(item => item.DoctorService)
            .Where(item => item.EndAt > timeProvider.GetUtcNow() &&
                (item.Status == AppointmentStatus.Booked ||
                 item.Status == AppointmentStatus.Confirmed) &&
                item.Services.Any(line => line.Status == AppointmentServiceStatus.Scheduled &&
                    line.DoctorService.DoctorId == doctorId))
            .ToArrayAsync(cancellationToken);
        Appointment[] affected = candidates.Where(appointment => appointment.Services.Any(line =>
            line.Status == AppointmentServiceStatus.Scheduled &&
            line.DoctorService.DoctorId == doctorId &&
            !AppointmentInfrastructureSupport.IsDoctorAvailable(line.SegmentStartAt,
                line.SegmentEndAt, schedules, exceptions))).ToArray();
        return Apply(affected, actorUserId, confirmed);
    }

    private Result Apply(Appointment[] affected, long actorUserId,
        bool confirmed)
    {
        if (affected.Length == 0) return Result.Success();
        if (!confirmed)
        {
            return Result.Failure(new ResultError("scheduling.conflict",
                $"سيؤثر هذا التغيير على {affected.Length} حجز. أعد الطلب بعد التأكيد.",
                new Dictionary<string, object?>
                {
                    ["affectedAppointmentCount"] = affected.Length,
                    ["affectedAppointmentIds"] = affected.Select(item => item.Id).ToArray()
                }));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (Appointment appointment in affected)
        {
            appointment.Suspend(actorUserId, now);
            dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId,
                "appointments.suspended", nameof(Appointment),
                appointment.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                now, "تغيير في توافر القسم أو الطبيب"));
        }
        return Result.Success();
    }

}
