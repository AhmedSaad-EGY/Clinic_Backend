namespace Clinic.Infrastructure.Catalog;

public sealed class CatalogQueryService : ICatalogQueryService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CatalogQueryService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CatalogPage<DepartmentModel>>> ListDepartmentsAsync(
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Department> query = _dbContext.Departments
            .AsNoTracking()
            .Include(item => item.Room)
            .Where(item => includeArchived || !item.IsArchived)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id);

        int count = await query.CountAsync(cancellationToken);
        List<Department> items = await ApplyPage(query, pageNumber, pageSize)
            .ToListAsync(cancellationToken);
        long[] departmentIds = items.Select(item => item.Id).ToArray();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        HashSet<long> stoppedDepartmentIds = (await _dbContext.DepartmentClosures
            .AsNoTracking()
            .Where(item => departmentIds.Contains(item.DepartmentId) &&
                           item.CancelledAt == null && item.StartAt <= now && now < item.EndAt)
            .Select(item => item.DepartmentId)
            .Distinct()
            .ToArrayAsync(cancellationToken))
            .ToHashSet();
        return Result.Success(Page(items.Select(item => CatalogMapper.Map(
            item,
            stoppedDepartmentIds.Contains(item.Id)
                ? DepartmentStatus.Stopped
                : DepartmentStatus.Active)), pageNumber, pageSize, count));
    }

    public async Task<Result<CatalogPage<SpecializationModel>>> ListSpecializationsAsync(
        long departmentId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        bool departmentExists = await _dbContext.Departments.AsNoTracking().AnyAsync(
            item => item.Id == departmentId && (includeArchived || !item.IsArchived),
            cancellationToken);
        if (!departmentExists)
        {
            return Result.Failure<CatalogPage<SpecializationModel>>(
                CatalogErrors.DepartmentNotFound);
        }

        IQueryable<Specialization> query = _dbContext.Specializations
            .AsNoTracking()
            .Where(item => item.DepartmentId == departmentId &&
                           (includeArchived || (!item.IsArchived && item.IsActive)))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id);
        int count = await query.CountAsync(cancellationToken);
        List<Specialization> items = await ApplyPage(query, pageNumber, pageSize)
            .ToListAsync(cancellationToken);
        return Result.Success(Page(items.Select(CatalogMapper.Map), pageNumber, pageSize, count));
    }

    public async Task<Result<CatalogPage<ServiceModel>>> ListServicesAsync(
        long specializationId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        bool specializationExists = await _dbContext.Specializations.AsNoTracking().AnyAsync(
            item => item.Id == specializationId && (includeArchived || !item.IsArchived),
            cancellationToken);
        if (!specializationExists)
        {
            return Result.Failure<CatalogPage<ServiceModel>>(
                CatalogErrors.SpecializationNotFound);
        }

        IQueryable<Service> query = _dbContext.Services
            .AsNoTracking()
            .Include(item => item.DeviceAssignments)
                .ThenInclude(item => item.Device)
            .Where(item => item.SpecializationId == specializationId &&
                           (includeArchived || (!item.IsArchived && item.IsActive)))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id);
        int count = await query.CountAsync(cancellationToken);
        List<Service> items = await ApplyPage(query, pageNumber, pageSize)
            .ToListAsync(cancellationToken);
        return Result.Success(Page(items.Select(CatalogMapper.Map), pageNumber, pageSize, count));
    }

    public async Task<Result<CatalogPage<DeviceModel>>> ListDevicesAsync(
        long departmentId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        bool departmentExists = await _dbContext.Departments.AsNoTracking().AnyAsync(
            item => item.Id == departmentId && (includeArchived || !item.IsArchived),
            cancellationToken);
        if (!departmentExists)
        {
            return Result.Failure<CatalogPage<DeviceModel>>(CatalogErrors.DepartmentNotFound);
        }

        IQueryable<Device> query = _dbContext.Devices
            .AsNoTracking()
            .Where(item => item.DepartmentId == departmentId &&
                           (includeArchived || (!item.IsArchived && item.IsActive)))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id);
        int count = await query.CountAsync(cancellationToken);
        List<Device> items = await ApplyPage(query, pageNumber, pageSize)
            .ToListAsync(cancellationToken);
        return Result.Success(Page(items.Select(CatalogMapper.Map), pageNumber, pageSize, count));
    }

    public async Task<Result<ServiceModel>> GetServiceAsync(
        long serviceId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        Service? service = await _dbContext.Services
            .AsNoTracking()
            .Include(item => item.DeviceAssignments)
                .ThenInclude(item => item.Device)
            .SingleOrDefaultAsync(
                item => item.Id == serviceId &&
                        (includeArchived || (!item.IsArchived && item.IsActive)),
                cancellationToken);
        return service is null
            ? Result.Failure<ServiceModel>(CatalogErrors.ServiceNotFound)
            : Result.Success(CatalogMapper.Map(service));
    }

    public async Task<Result<CatalogPage<ServicePriceHistoryModel>>> ListPriceHistoryAsync(
        long serviceId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        bool serviceExists = await _dbContext.Services.AsNoTracking().AnyAsync(
            item => item.Id == serviceId,
            cancellationToken);
        if (!serviceExists)
        {
            return Result.Failure<CatalogPage<ServicePriceHistoryModel>>(
                CatalogErrors.ServiceNotFound);
        }

        IQueryable<ServicePriceHistory> query = _dbContext.ServicePriceHistory
            .AsNoTracking()
            .Where(item => item.ServiceId == serviceId)
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenByDescending(item => item.Id);
        int count = await query.CountAsync(cancellationToken);
        List<ServicePriceHistoryModel> items = await ApplyPage(query, pageNumber, pageSize)
            .Select(item => new ServicePriceHistoryModel(
                item.Id,
                item.UnitPrice,
                item.EffectiveFrom,
                item.EffectiveTo,
                item.ChangedByUserId,
                item.ChangedAt))
            .ToListAsync(cancellationToken);
        return Result.Success(Page(items, pageNumber, pageSize, count));
    }

    private static IQueryable<T> ApplyPage<T>(
        IQueryable<T> query,
        int pageNumber,
        int pageSize) =>
        query.Skip((pageNumber - 1) * pageSize).Take(pageSize);

    private static CatalogPage<T> Page<T>(
        IEnumerable<T> items,
        int pageNumber,
        int pageSize,
        int totalCount) =>
        new(items.ToArray(), pageNumber, pageSize, totalCount);
}
