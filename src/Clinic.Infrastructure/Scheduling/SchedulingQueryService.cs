namespace Clinic.Infrastructure.Scheduling;

public sealed class SchedulingQueryService : ISchedulingQueryService
{
    private const int AvailabilityPreviewSize = 10;
    private readonly ClinicDbContext _dbContext;

    public SchedulingQueryService(ClinicDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<SchedulingPage<DoctorModel>>> ListDoctorsAsync(
        long? departmentId,
        long? serviceId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Doctor> query = _dbContext.Doctors.AsNoTracking()
            .Where(item => includeArchived || (!item.IsArchived && item.IsActive));
        if (departmentId.HasValue)
        {
            query = query.Where(item => item.DepartmentId == departmentId.Value);
        }

        if (serviceId.HasValue)
        {
            query = query.Where(item => _dbContext.DoctorServices.Any(assignment =>
                assignment.DoctorId == item.Id && assignment.ServiceId == serviceId.Value &&
                assignment.IsActive));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<Doctor> doctors = await query.OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        IReadOnlyCollection<DoctorModel> items = await MapDoctorsAsync(
            doctors,
            cancellationToken);
        return Result.Success(new SchedulingPage<DoctorModel>(
            items,
            pageNumber,
            pageSize,
            totalCount));
    }

    public async Task<Result<DoctorModel>> GetDoctorAsync(
        long doctorId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        Doctor? doctor = await _dbContext.Doctors.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == doctorId && (includeArchived || !item.IsArchived),
            cancellationToken);
        if (doctor is null)
        {
            return Result.Failure<DoctorModel>(SchedulingErrors.DoctorNotFound);
        }

        IReadOnlyCollection<DoctorModel> mapped = await MapDoctorsAsync([doctor], cancellationToken);
        return Result.Success(mapped.Single());
    }

    public async Task<Result<SchedulingPage<DoctorScheduleModel>>> ListSchedulesAsync(
        long doctorId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await DoctorExistsAsync(doctorId, cancellationToken))
        {
            return Result.Failure<SchedulingPage<DoctorScheduleModel>>(
                SchedulingErrors.DoctorNotFound);
        }

        IQueryable<DoctorSchedule> query = _dbContext.DoctorSchedules.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.IsActive);
        if (fromDate.HasValue)
        {
            query = query.Where(item => !item.EffectiveTo.HasValue || item.EffectiveTo >= fromDate);
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.EffectiveFrom <= toDate);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        DoctorSchedule[] schedules = await query
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartTime)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new SchedulingPage<DoctorScheduleModel>(
            schedules.Select(SchedulingMapper.Map).ToArray(),
            pageNumber,
            pageSize,
            totalCount));
    }

    public async Task<Result<SchedulingPage<DoctorExceptionModel>>> ListExceptionsAsync(
        long doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (fromDate > toDate)
        {
            return Result.Failure<SchedulingPage<DoctorExceptionModel>>(
                SchedulingErrors.Validation("تاريخ البداية يجب ألا يتجاوز تاريخ النهاية."));
        }

        if (!await DoctorExistsAsync(doctorId, cancellationToken))
        {
            return Result.Failure<SchedulingPage<DoctorExceptionModel>>(
                SchedulingErrors.DoctorNotFound);
        }

        IQueryable<DoctorScheduleOverride> query = _dbContext.DoctorExceptions.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.ExceptionDate >= fromDate &&
                           item.ExceptionDate <= toDate &&
                           (includeCancelled || item.CancelledAt == null));
        int totalCount = await query.CountAsync(cancellationToken);
        DoctorScheduleOverride[] items = await query
            .OrderBy(item => item.ExceptionDate)
            .ThenBy(item => item.StartTime)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new SchedulingPage<DoctorExceptionModel>(
            items.Select(SchedulingMapper.Map).ToArray(),
            pageNumber,
            pageSize,
            totalCount));
    }

    public async Task<Result<DepartmentAvailabilityModel>> GetDepartmentAvailabilityAsync(
        long departmentId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.Departments.AsNoTracking().AnyAsync(
            item => item.Id == departmentId && !item.IsArchived,
            cancellationToken);
        if (!exists)
        {
            return Result.Failure<DepartmentAvailabilityModel>(SchedulingErrors.DepartmentNotFound);
        }

        DateTimeOffset instant = at.ToUniversalTime();
        DepartmentClosure? current = await _dbContext.DepartmentClosures.AsNoTracking()
            .Where(item => item.DepartmentId == departmentId && item.CancelledAt == null &&
                           item.StartAt <= instant && instant < item.EndAt)
            .SingleOrDefaultAsync(cancellationToken);
        DepartmentClosure[] upcoming = await _dbContext.DepartmentClosures.AsNoTracking()
            .Where(item => item.DepartmentId == departmentId && item.CancelledAt == null &&
                           item.StartAt > instant)
            .OrderBy(item => item.StartAt)
            .Take(AvailabilityPreviewSize + 1)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new DepartmentAvailabilityModel(
            departmentId,
            current is null ? DepartmentStatus.Active : DepartmentStatus.Stopped,
            current is null ? null : SchedulingMapper.Map(current),
            upcoming.Take(AvailabilityPreviewSize).Select(SchedulingMapper.Map).ToArray(),
            upcoming.Length > AvailabilityPreviewSize));
    }

    public async Task<Result<SchedulingPage<DepartmentClosureModel>>> ListClosuresAsync(
        long departmentId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        bool includeCancelled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Departments.AsNoTracking().AnyAsync(
                item => item.Id == departmentId, cancellationToken))
        {
            return Result.Failure<SchedulingPage<DepartmentClosureModel>>(
                SchedulingErrors.DepartmentNotFound);
        }

        IQueryable<DepartmentClosure> query = _dbContext.DepartmentClosures.AsNoTracking()
            .Where(item => item.DepartmentId == departmentId &&
                           (includeCancelled || item.CancelledAt == null));
        if (fromDate.HasValue)
        {
            query = query.Where(item => item.EndAt > fromDate.Value.ToUniversalTime());
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.StartAt < toDate.Value.ToUniversalTime());
        }

        int totalCount = await query.CountAsync(cancellationToken);
        DepartmentClosure[] items = await query.OrderBy(item => item.StartAt)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new SchedulingPage<DepartmentClosureModel>(
            items.Select(SchedulingMapper.Map).ToArray(),
            pageNumber,
            pageSize,
            totalCount));
    }

    private Task<bool> DoctorExistsAsync(long doctorId, CancellationToken cancellationToken) =>
        _dbContext.Doctors.AsNoTracking().AnyAsync(item => item.Id == doctorId, cancellationToken);

    private async Task<IReadOnlyCollection<DoctorModel>> MapDoctorsAsync(
        IReadOnlyCollection<Doctor> doctors,
        CancellationToken cancellationToken)
    {
        long[] ids = doctors.Select(item => item.Id).ToArray();
        DoctorServiceProjection[] services = await _dbContext.DoctorServices.AsNoTracking()
            .Where(item => ids.Contains(item.DoctorId) && item.IsActive)
            .OrderBy(item => item.Service.Name)
            .Select(item => new DoctorServiceProjection(
                item.DoctorId,
                new DoctorServiceModel(item.Id, item.ServiceId, item.Service.Name, item.IsActive)))
            .ToArrayAsync(cancellationToken);
        return doctors.Select(doctor => SchedulingMapper.Map(
            doctor,
            services.Where(item => item.DoctorId == doctor.Id).Select(item => item.Model)))
            .ToArray();
    }

    private sealed record DoctorServiceProjection(long DoctorId, DoctorServiceModel Model);
}
