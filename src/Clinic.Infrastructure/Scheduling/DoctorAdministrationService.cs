namespace Clinic.Infrastructure.Scheduling;

public sealed class DoctorAdministrationService : IDoctorAdministrationService
{
    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DoctorAdministrationService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DoctorModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        string name,
        string? phone,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        Doctor doctor = Doctor.Create(departmentId, name, phone, now);
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId, cancellationToken);
                bool departmentExists = await _dbContext.Departments.AsNoTracking().AnyAsync(
                    item => item.Id == departmentId && !item.IsArchived,
                    cancellationToken);
                if (!departmentExists)
                {
                    return Result.Failure<DoctorModel>(SchedulingErrors.DepartmentNotFound);
                }

                _dbContext.Doctors.Add(doctor);
                await _dbContext.SaveChangesAsync(cancellationToken);
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, SchedulingAuditActions.DoctorCreated,
                    nameof(Doctor), doctor.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(SchedulingMapper.Map(doctor));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DoctorModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<DoctorModel>> UpdateAsync(
        long actorUserId,
        long doctorId,
        string name,
        string? phone,
        bool isActive,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        Doctor? doctor = await _dbContext.Doctors.SingleOrDefaultAsync(
            item => item.Id == doctorId && !item.IsArchived,
            cancellationToken);
        if (doctor is null)
        {
            return Result.Failure<DoctorModel>(SchedulingErrors.DoctorNotFound);
        }

        if (!SchedulingInfrastructureSupport.MatchesVersion(doctor.RowVersion, rowVersion))
        {
            return Result.Failure<DoctorModel>(SchedulingErrors.ConcurrencyConflict);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        doctor.Update(name, phone, isActive, now);
        SchedulingInfrastructureSupport.AddAudit(
            _dbContext, actorUserId, SchedulingAuditActions.DoctorUpdated,
            nameof(Doctor), doctor.Id, now);
        return await SaveDoctorAsync(doctor, cancellationToken);
    }

    public async Task<Result<DoctorModel>> ReplaceServicesAsync(
        long actorUserId,
        long doctorId,
        IReadOnlyCollection<long> serviceIds,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        long[] requestedIds = serviceIds.Distinct().ToArray();
        if (requestedIds.Length != serviceIds.Count)
        {
            return Result.Failure<DoctorModel>(SchedulingErrors.Validation(
                "لا يمكن تكرار الخدمة للطبيب."));
        }

        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(
                    _dbContext, doctorId, cancellationToken);
                foreach (long serviceId in requestedIds.Order())
                {
                    await TransactionalResourceLock.AcquireServiceAsync(
                        _dbContext, serviceId, cancellationToken);
                }

                Doctor? doctor = await _dbContext.Doctors.SingleOrDefaultAsync(
                    item => item.Id == doctorId && !item.IsArchived,
                    cancellationToken);
                if (doctor is null)
                {
                    return Result.Failure<DoctorModel>(SchedulingErrors.DoctorNotFound);
                }

                if (!SchedulingInfrastructureSupport.MatchesVersion(doctor.RowVersion, rowVersion))
                {
                    return Result.Failure<DoctorModel>(SchedulingErrors.ConcurrencyConflict);
                }

                List<Service> services = await _dbContext.Services
                    .Where(item => requestedIds.Contains(item.Id) &&
                                   !item.IsArchived && item.IsActive)
                    .ToListAsync(cancellationToken);
                if (services.Count != requestedIds.Length)
                {
                    return Result.Failure<DoctorModel>(SchedulingErrors.ServiceNotFound);
                }

                if (services.Any(item => item.DepartmentId != doctor.DepartmentId))
                {
                    return Result.Failure<DoctorModel>(SchedulingErrors.Validation(
                        "يجب أن تنتمي كل خدمات الطبيب إلى قسمه."));
                }

                List<DoctorService> existing = await _dbContext.DoctorServices
                    .Where(item => item.DoctorId == doctorId)
                    .ToListAsync(cancellationToken);
                foreach (DoctorService assignment in existing)
                {
                    if (requestedIds.Contains(assignment.ServiceId))
                    {
                        assignment.Activate();
                    }
                    else
                    {
                        assignment.Deactivate();
                    }
                }

                HashSet<long> existingIds = existing.Select(item => item.ServiceId).ToHashSet();
                foreach (Service service in services.Where(item => !existingIds.Contains(item.Id)))
                {
                    _dbContext.DoctorServices.Add(DoctorService.Create(doctor, service));
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                doctor.Touch(now);
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, SchedulingAuditActions.DoctorServicesReplaced,
                    nameof(Doctor), doctor.Id, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                IReadOnlyCollection<DoctorServiceModel> models = await LoadServiceModelsAsync(
                    doctorId, cancellationToken);
                return Result.Success(SchedulingMapper.Map(doctor, models));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DoctorModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchiveAsync(
        long actorUserId,
        long doctorId,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateOnly clinicDate = GetClinicDate(now);
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDoctorAsync(
                    _dbContext, doctorId, cancellationToken);
                Doctor? doctor = await _dbContext.Doctors.SingleOrDefaultAsync(
                    item => item.Id == doctorId && !item.IsArchived,
                    cancellationToken);
                if (doctor is null)
                {
                    return Result.Failure(SchedulingErrors.DoctorNotFound);
                }

                if (!SchedulingInfrastructureSupport.MatchesVersion(
                        doctor.RowVersion,
                        rowVersion))
                {
                    return Result.Failure(SchedulingErrors.ConcurrencyConflict);
                }

                doctor.Archive(now);
                List<DoctorService> assignments = await _dbContext.DoctorServices
                    .Where(item => item.DoctorId == doctorId && item.IsActive)
                    .ToListAsync(cancellationToken);
                assignments.ForEach(item => item.Deactivate());
                List<DoctorSchedule> schedules = await _dbContext.DoctorSchedules
                    .Where(item => item.DoctorId == doctorId && item.IsActive)
                    .ToListAsync(cancellationToken);
                schedules.ForEach(item => item.Deactivate());
                List<DoctorScheduleOverride> exceptions = await _dbContext.DoctorExceptions
                    .Where(item => item.DoctorId == doctorId && item.CancelledAt == null &&
                                   item.ExceptionDate >= clinicDate)
                    .ToListAsync(cancellationToken);
                exceptions.ForEach(item => item.Cancel(actorUserId, now));
                SchedulingInfrastructureSupport.AddAudit(
                    _dbContext, actorUserId, SchedulingAuditActions.DoctorArchived,
                    nameof(Doctor), doctor.Id, now);
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

    private async Task<Result<DoctorModel>> SaveDoctorAsync(
        Doctor doctor,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            IReadOnlyCollection<DoctorServiceModel> services = await LoadServiceModelsAsync(
                doctor.Id, cancellationToken);
            return Result.Success(SchedulingMapper.Map(doctor, services));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DoctorModel>(
                SchedulingInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    private static DateOnly GetClinicDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ClinicTimeZone).DateTime);

    private async Task<IReadOnlyCollection<DoctorServiceModel>> LoadServiceModelsAsync(
        long doctorId,
        CancellationToken cancellationToken) =>
        await _dbContext.DoctorServices.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.IsActive)
            .OrderBy(item => item.Service.Name)
            .Select(item => new DoctorServiceModel(
                item.Id, item.ServiceId, item.Service.Name, item.IsActive))
            .ToArrayAsync(cancellationToken);
}
