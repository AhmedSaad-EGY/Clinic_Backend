namespace Clinic.Infrastructure.Scheduling;

public sealed class ScheduleAdministrationService : IScheduleAdministrationService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly AppointmentImpactService _appointmentImpact;

    public ScheduleAdministrationService(ClinicDbContext dbContext, TimeProvider timeProvider,
        AppointmentImpactService appointmentImpact)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _appointmentImpact = appointmentImpact;
    }

    public async Task<Result<DoctorScheduleModel>> CreateScheduleAsync(
        long actorUserId,
        long doctorId,
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        CancellationToken cancellationToken)
    {
        DoctorSchedule schedule = DoctorSchedule.Create(
            doctorId, dayOfWeek, startTime, endTime, effectiveFrom, effectiveTo);
        return await ExecuteScheduleMutationAsync(
            schedule,
            actorUserId,
            SchedulingAuditActions.ScheduleCreated,
            excludeScheduleId: null,
            cancellationToken);
    }

    public async Task<Result<DoctorScheduleModel>> UpdateScheduleAsync(
        long actorUserId,
        long scheduleId,
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        byte[] rowVersion,
        bool confirmAffectedAppointments,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                long? doctorId = await _dbContext.DoctorSchedules.AsNoTracking()
                    .Where(item => item.Id == scheduleId && item.IsActive)
                    .Select(item => (long?)item.DoctorId).SingleOrDefaultAsync(cancellationToken);
                if (!doctorId.HasValue)
                    return Result.Failure<DoctorScheduleModel>(SchedulingErrors.ScheduleNotFound);

                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(_dbContext, doctorId.Value,
                    cancellationToken);
                DoctorSchedule? schedule = await _dbContext.DoctorSchedules.SingleOrDefaultAsync(
                    item => item.Id == scheduleId && item.IsActive, cancellationToken);
                if (schedule is null)
                    return Result.Failure<DoctorScheduleModel>(SchedulingErrors.ScheduleNotFound);
                if (!SchedulingInfrastructureSupport.MatchesVersion(schedule.RowVersion, rowVersion))
                    return Result.Failure<DoctorScheduleModel>(SchedulingErrors.ConcurrencyConflict);

                schedule.Update(dayOfWeek, startTime, endTime, effectiveFrom, effectiveTo);
                Result impact = await _appointmentImpact.ApplyDoctorScheduleChangeAsync(actorUserId,
                    schedule.DoctorId, confirmAffectedAppointments, cancellationToken);
                if (impact.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DoctorScheduleModel>(impact.Error);
                }

                bool overlap = await _dbContext.DoctorSchedules.AnyAsync(item =>
                    item.DoctorId == schedule.DoctorId && item.Id != schedule.Id &&
                    item.DayOfWeek == schedule.DayOfWeek && item.IsActive &&
                    item.StartTime < schedule.EndTime && schedule.StartTime < item.EndTime &&
                    (!schedule.EffectiveTo.HasValue || item.EffectiveFrom <= schedule.EffectiveTo) &&
                    (!item.EffectiveTo.HasValue || schedule.EffectiveFrom <= item.EffectiveTo),
                    cancellationToken);
                if (overlap)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DoctorScheduleModel>(SchedulingErrors.Conflict(
                        "توجد فترة عمل متداخلة للطبيب في اليوم ونطاق التاريخ المحددين."));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                SchedulingInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    SchedulingAuditActions.ScheduleUpdated, nameof(DoctorSchedule), schedule.Id,
                    _timeProvider.GetUtcNow());
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(SchedulingMapper.Map(schedule));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DoctorScheduleModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> DeactivateScheduleAsync(
        long actorUserId,
        long scheduleId,
        byte[] rowVersion,
        bool confirmAffectedAppointments,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                long? doctorId = await _dbContext.DoctorSchedules.AsNoTracking()
                    .Where(item => item.Id == scheduleId && item.IsActive)
                    .Select(item => (long?)item.DoctorId).SingleOrDefaultAsync(cancellationToken);
                if (!doctorId.HasValue) return Result.Failure(SchedulingErrors.ScheduleNotFound);

                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(_dbContext, doctorId.Value,
                    cancellationToken);
                DoctorSchedule? schedule = await _dbContext.DoctorSchedules.SingleOrDefaultAsync(
                    item => item.Id == scheduleId && item.IsActive, cancellationToken);
                if (schedule is null) return Result.Failure(SchedulingErrors.ScheduleNotFound);
                if (!SchedulingInfrastructureSupport.MatchesVersion(schedule.RowVersion, rowVersion))
                    return Result.Failure(SchedulingErrors.ConcurrencyConflict);

                schedule.Deactivate();
                Result impact = await _appointmentImpact.ApplyDoctorScheduleChangeAsync(actorUserId,
                    schedule.DoctorId, confirmAffectedAppointments, cancellationToken);
                if (impact.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return impact;
                }
                SchedulingInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    SchedulingAuditActions.ScheduleDeactivated, nameof(DoctorSchedule), schedule.Id,
                    _timeProvider.GetUtcNow());
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<DoctorExceptionModel>> CreateExceptionAsync(
        long actorUserId,
        long doctorId,
        DateOnly exceptionDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        DoctorExceptionType type,
        string? reason,
        bool confirmAffectedAppointments,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DoctorScheduleOverride exception = DoctorScheduleOverride.Create(
            doctorId, exceptionDate, startTime, endTime, type, reason, actorUserId, now);
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(
                    _dbContext, doctorId, cancellationToken);
                if (!await ActiveDoctorExistsAsync(doctorId, cancellationToken))
                {
                    return Result.Failure<DoctorExceptionModel>(
                        SchedulingErrors.DoctorNotFound);
                }

                if (type == DoctorExceptionType.Unavailable)
                {
                    Result impact = await _appointmentImpact.ApplyDoctorExceptionAsync(
                        actorUserId, doctorId, exceptionDate, startTime, endTime,
                        confirmAffectedAppointments, cancellationToken);
                    if (impact.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<DoctorExceptionModel>(impact.Error);
                    }
                }

                bool overlap = await _dbContext.DoctorExceptions.AnyAsync(
                    item => item.DoctorId == doctorId &&
                            item.ExceptionDate == exceptionDate &&
                            item.CancelledAt == null &&
                            (!startTime.HasValue || !item.StartTime.HasValue ||
                             (item.StartTime < endTime && startTime < item.EndTime)),
                    cancellationToken);
                if (overlap)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DoctorExceptionModel>(SchedulingErrors.Conflict(
                        "يوجد استثناء آخر متداخل للطبيب في هذا اليوم."));
                }

                _dbContext.DoctorExceptions.Add(exception);
                await _dbContext.SaveChangesAsync(cancellationToken);
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, SchedulingAuditActions.ExceptionCreated,
                    nameof(DoctorScheduleOverride), exception.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(SchedulingMapper.Map(exception));
            });
        }
        catch (DbUpdateException dbException)
        {
            return Result.Failure<DoctorExceptionModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(dbException));
        }
    }

    public async Task<Result> CancelExceptionAsync(
        long actorUserId,
        long exceptionId,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        DoctorScheduleOverride? exception = await _dbContext.DoctorExceptions.SingleOrDefaultAsync(
            item => item.Id == exceptionId && item.CancelledAt == null,
            cancellationToken);
        if (exception is null)
        {
            return Result.Failure(SchedulingErrors.ExceptionNotFound);
        }

        if (!SchedulingInfrastructureSupport.MatchesVersion(exception.RowVersion, rowVersion))
        {
            return Result.Failure(SchedulingErrors.ConcurrencyConflict);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        exception.Cancel(actorUserId, now);
        SchedulingInfrastructureSupport.AddAudit(
            _dbContext, actorUserId, SchedulingAuditActions.ExceptionCancelled,
            nameof(DoctorScheduleOverride), exception.Id, now);
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result<DepartmentClosureModel>> CreateClosureAsync(
        long actorUserId,
        long departmentId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string reason,
        bool confirmAffectedAppointments,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DepartmentClosure closure = DepartmentClosure.Create(
            departmentId, startAt, endAt, reason, actorUserId, now);
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId, cancellationToken);
                bool exists = await _dbContext.Departments.AsNoTracking().AnyAsync(
                    item => item.Id == departmentId && !item.IsArchived,
                    cancellationToken);
                if (!exists)
                {
                    return Result.Failure<DepartmentClosureModel>(
                        SchedulingErrors.DepartmentNotFound);
                }

                Result impact = await _appointmentImpact.ApplyDepartmentClosureAsync(
                    actorUserId, departmentId, startAt, endAt,
                    confirmAffectedAppointments, cancellationToken);
                if (impact.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DepartmentClosureModel>(impact.Error);
                }

                bool overlap = await _dbContext.DepartmentClosures.AnyAsync(
                    item => item.DepartmentId == departmentId && item.CancelledAt == null &&
                            item.StartAt < closure.EndAt && closure.StartAt < item.EndAt,
                    cancellationToken);
                if (overlap)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DepartmentClosureModel>(SchedulingErrors.Conflict(
                        "توجد فترة إيقاف أخرى متداخلة لهذا القسم."));
                }

                _dbContext.DepartmentClosures.Add(closure);
                await _dbContext.SaveChangesAsync(cancellationToken);
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, SchedulingAuditActions.ClosureCreated,
                    nameof(DepartmentClosure), closure.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(SchedulingMapper.Map(closure));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DepartmentClosureModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> CancelClosureAsync(
        long actorUserId,
        long closureId,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        DepartmentClosure? closure = await _dbContext.DepartmentClosures.SingleOrDefaultAsync(
            item => item.Id == closureId && item.CancelledAt == null,
            cancellationToken);
        if (closure is null)
        {
            return Result.Failure(SchedulingErrors.ClosureNotFound);
        }

        if (!SchedulingInfrastructureSupport.MatchesVersion(closure.RowVersion, rowVersion))
        {
            return Result.Failure(SchedulingErrors.ConcurrencyConflict);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        closure.Cancel(actorUserId, now);
        SchedulingInfrastructureSupport.AddAudit(
            _dbContext, actorUserId, SchedulingAuditActions.ClosureCancelled,
            nameof(DepartmentClosure), closure.Id, now);
        return await SaveAsync(cancellationToken);
    }

    private async Task<Result<DoctorScheduleModel>> ExecuteScheduleMutationAsync(
        DoctorSchedule schedule,
        long actorUserId,
        string auditAction,
        long? excludeScheduleId,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(
                    _dbContext, schedule.DoctorId, cancellationToken);
                if (!await ActiveDoctorExistsAsync(schedule.DoctorId, cancellationToken))
                {
                    return Result.Failure<DoctorScheduleModel>(
                        SchedulingErrors.DoctorNotFound);
                }

                bool overlap = await _dbContext.DoctorSchedules.AnyAsync(
                    item => item.DoctorId == schedule.DoctorId &&
                            item.DayOfWeek == schedule.DayOfWeek && item.IsActive &&
                            (!excludeScheduleId.HasValue || item.Id != excludeScheduleId.Value) &&
                            item.StartTime < schedule.EndTime && schedule.StartTime < item.EndTime &&
                            (!schedule.EffectiveTo.HasValue || item.EffectiveFrom <= schedule.EffectiveTo) &&
                            (!item.EffectiveTo.HasValue || schedule.EffectiveFrom <= item.EffectiveTo),
                    cancellationToken);
                if (overlap)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<DoctorScheduleModel>(SchedulingErrors.Conflict(
                        "توجد فترة عمل متداخلة للطبيب في اليوم ونطاق التاريخ المحددين."));
                }

                if (schedule.Id == 0)
                {
                    _dbContext.DoctorSchedules.Add(schedule);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, auditAction,
                    nameof(DoctorSchedule), schedule.Id, _timeProvider.GetUtcNow());
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(SchedulingMapper.Map(schedule));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DoctorScheduleModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    private Task<bool> ActiveDoctorExistsAsync(long doctorId, CancellationToken cancellationToken) =>
        _dbContext.Doctors.AsNoTracking().AnyAsync(
            item => item.Id == doctorId && !item.IsArchived && item.IsActive,
            cancellationToken);

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }
}
