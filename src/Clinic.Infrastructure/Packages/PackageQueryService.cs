namespace Clinic.Infrastructure.Packages;

public sealed class PackageQueryService : IPackageQueryService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly DiscountResolver _discountResolver;

    public PackageQueryService(ClinicDbContext dbContext, TimeProvider timeProvider,
        DiscountResolver discountResolver)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _discountResolver = discountResolver;
    }

    public async Task<Result<PackagePage>> SearchAdminAsync(AdminPackageFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<Package> query = _dbContext.Packages.AsNoTracking()
            .Where(item => filter.IncludeArchived || !item.IsArchived);
        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(item => item.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(item => item.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim();
            query = query.Where(item => item.Name.Contains(search));
        }

        return Result.Success(await PageAsync(query, filter.PageNumber, filter.PageSize,
            cancellationToken));
    }

    public async Task<Result<PackageModel>> GetAdminAsync(long packageId,
        CancellationToken cancellationToken)
    {
        Package? package = await FullQuery().SingleOrDefaultAsync(item => item.Id == packageId,
            cancellationToken);
        if (package is null)
        {
            return Result.Failure<PackageModel>(PackageErrors.NotFound);
        }
        IReadOnlyDictionary<long, DiscountQuote> discounts = await _discountResolver
            .QuotePackagesAsync([package], _timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success(PackageInfrastructureSupport.Map(package,
            await IsDepartmentStoppedAsync(package.DepartmentId, cancellationToken),
            discounts.GetValueOrDefault(package.Id)));
    }

    public async Task<Result<PackagePage>> SearchAvailableAsync(PackageCatalogFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<Package> query = AvailableQuery();
        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(item => item.DepartmentId == filter.DepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim();
            query = query.Where(item => item.Name.Contains(search));
        }

        return Result.Success(await PageAsync(query, filter.PageNumber, filter.PageSize,
            cancellationToken));
    }

    public async Task<Result<PackageModel>> GetAvailableAsync(long packageId,
        CancellationToken cancellationToken)
    {
        Package? package = await FullQuery(AvailableQuery())
            .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken);
        if (package is null)
        {
            return Result.Failure<PackageModel>(PackageErrors.NotFound);
        }
        IReadOnlyDictionary<long, DiscountQuote> discounts = await _discountResolver
            .QuotePackagesAsync([package], _timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success(PackageInfrastructureSupport.Map(package,
            departmentStopped: false, discounts.GetValueOrDefault(package.Id)));
    }

    private IQueryable<Package> AvailableQuery()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return _dbContext.Packages.AsNoTracking().Where(item =>
            item.IsActive && !item.IsArchived && !item.Department.IsArchived &&
            item.ActivationGraceDays != null && item.UsageDurationDays != null &&
            item.Services.Any(line => line.IsActive) &&
            item.Services.Where(line => line.IsActive).All(line =>
                line.Service.IsActive && !line.Service.IsArchived &&
                line.Service.PricingMode == Domain.Catalog.PricingMode.Fixed) &&
            !_dbContext.DepartmentClosures.Any(closure =>
                closure.DepartmentId == item.DepartmentId && closure.CancelledAt == null &&
                closure.StartAt <= now && now < closure.EndAt));
    }

    private async Task<PackagePage> PageAsync(IQueryable<Package> query, int pageNumber,
        int pageSize, CancellationToken cancellationToken)
    {
        int count = await query.CountAsync(cancellationToken);
        List<Package> packages = await FullQuery(query).OrderBy(item => item.Name)
            .ThenBy(item => item.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        long[] stoppedIds = await StoppedDepartmentIdsAsync(
            packages.Select(item => item.DepartmentId).Distinct().ToArray(), cancellationToken);
        HashSet<long> stopped = stoppedIds.ToHashSet();
        IReadOnlyDictionary<long, DiscountQuote> discounts = await _discountResolver
            .QuotePackagesAsync(packages, _timeProvider.GetUtcNow(), cancellationToken);
        return new PackagePage(packages.Select(item => PackageInfrastructureSupport.Map(item,
            stopped.Contains(item.DepartmentId), discounts.GetValueOrDefault(item.Id)))
            .ToArray(), pageNumber, pageSize, count);
    }

    private IQueryable<Package> FullQuery(IQueryable<Package>? source = null) =>
        (source ?? _dbContext.Packages.AsNoTracking())
            .Include(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.Service)
                .ThenInclude(item => item.Specialization)
            .AsSplitQuery();

    private async Task<bool> IsDepartmentStoppedAsync(long departmentId,
        CancellationToken cancellationToken) =>
        (await StoppedDepartmentIdsAsync([departmentId], cancellationToken)).Length != 0;

    private Task<long[]> StoppedDepartmentIdsAsync(long[] departmentIds,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return _dbContext.DepartmentClosures.AsNoTracking().Where(item =>
                departmentIds.Contains(item.DepartmentId) && item.CancelledAt == null &&
                item.StartAt <= now && now < item.EndAt)
            .Select(item => item.DepartmentId).Distinct().ToArrayAsync(cancellationToken);
    }
}
