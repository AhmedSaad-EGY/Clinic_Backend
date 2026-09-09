using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Domain.Packages;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Packages;

public sealed class PatientPackageQueryService : IPatientPackageQueryService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PatientPackageQueryService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PatientPackagePage>> ListForPatientAsync(long patientId,
        int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.Patients.AsNoTracking().AnyAsync(
            item => item.Id == patientId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<PatientPackagePage>(PackageErrors.PatientNotFound);
        }

        return Result.Success(await PageAsync(_dbContext.PatientPackages.AsNoTracking()
            .Where(item => item.PatientId == patientId), pageNumber, pageSize,
            cancellationToken));
    }

    public async Task<Result<PatientPackageModel>> GetAsync(long patientPackageId,
        CancellationToken cancellationToken)
    {
        PatientPackage? patientPackage = await FullQuery().SingleOrDefaultAsync(
            item => item.Id == patientPackageId, cancellationToken);
        if (patientPackage is null)
        {
            return Result.Failure<PatientPackageModel>(PackageErrors.PatientPackageNotFound);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool stopped = await IsDepartmentStoppedAsync(patientPackage.DepartmentId, now,
            cancellationToken);
        return Result.Success(PatientPackageInfrastructureSupport.Map(patientPackage, stopped,
            now));
    }

    public async Task<Result<IReadOnlyCollection<PackageSessionModel>>> ListSessionsAsync(
        long patientPackageId, CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.PatientPackages.AsNoTracking().AnyAsync(
            item => item.Id == patientPackageId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<IReadOnlyCollection<PackageSessionModel>>(
                PackageErrors.PatientPackageNotFound);
        }

        PackageSessionModel[] sessions = await _dbContext.PackageSessions.AsNoTracking()
            .Where(item => item.PatientPackageId == patientPackageId)
            .OrderBy(item => item.PatientPackageService.ServiceNameSnapshot)
            .ThenBy(item => item.SequenceNumber)
            .Select(item => new PackageSessionModel(item.Id, item.PatientPackageServiceId,
                item.ServiceId, item.PatientPackageService.ServiceNameSnapshot,
                item.SequenceNumber, item.UnitPriceSnapshot, item.Status, item.ReservedAt,
                item.ConsumedAt, Convert.ToBase64String(item.RowVersion)))
            .ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<PackageSessionModel>>(sessions);
    }

    public async Task<Result<PatientPackagePage>> SearchAdminAsync(
        AdminPatientPackageFilter filter, CancellationToken cancellationToken)
    {
        IQueryable<PatientPackage> query = _dbContext.PatientPackages.AsNoTracking();
        if (filter.PatientId.HasValue)
        {
            query = query.Where(item => item.PatientId == filter.PatientId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(item => item.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.PaymentStatus.HasValue)
        {
            query = query.Where(item => item.PaymentStatus == filter.PaymentStatus.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(item => item.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim();
            query = query.Where(item => item.Patient.FullName.Contains(search) ||
                item.PackageNameSnapshot.Contains(search));
        }

        if (filter.IsUsable.HasValue)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            query = query.Where(item => (item.Status == PatientPackageStatus.Active &&
                item.PaymentStatus != PatientPackagePaymentStatus.Unpaid &&
                !item.Package.Department.IsArchived &&
                (item.FirstUsedAt != null || item.ActivationDeadlineAt > now) &&
                (item.ExpiresAt == null || item.ExpiresAt > now) &&
                item.Services.All(line => !line.SourcePackageService.Service.IsArchived &&
                    line.SourcePackageService.Service.IsActive) &&
                !_dbContext.DepartmentClosures.Any(closure =>
                    closure.DepartmentId == item.DepartmentId && closure.CancelledAt == null &&
                    closure.StartAt <= now && now < closure.EndAt)) == filter.IsUsable.Value);
        }

        return Result.Success(await PageAsync(query, filter.PageNumber, filter.PageSize,
            cancellationToken));
    }

    private async Task<PatientPackagePage> PageAsync(IQueryable<PatientPackage> query,
        int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        int count = await query.CountAsync(cancellationToken);
        List<PatientPackage> patientPackages = await FullQuery(query)
            .OrderByDescending(item => item.RegisteredAt).ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        long[] departmentIds = patientPackages.Select(item => item.DepartmentId).Distinct()
            .ToArray();
        HashSet<long> stopped = (await _dbContext.DepartmentClosures.AsNoTracking()
            .Where(item => departmentIds.Contains(item.DepartmentId) &&
                item.CancelledAt == null && item.StartAt <= now && now < item.EndAt)
            .Select(item => item.DepartmentId).Distinct().ToArrayAsync(cancellationToken))
            .ToHashSet();
        return new PatientPackagePage(patientPackages.Select(item =>
            PatientPackageInfrastructureSupport.Map(item, stopped.Contains(item.DepartmentId),
                now)).ToArray(), pageNumber, pageSize, count);
    }

    private IQueryable<PatientPackage> FullQuery(IQueryable<PatientPackage>? source = null) =>
        (source ?? _dbContext.PatientPackages.AsNoTracking())
            .Include(item => item.Patient)
            .Include(item => item.Package).ThenInclude(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.SourcePackageService)
                .ThenInclude(item => item.Service)
            .Include(item => item.Services).ThenInclude(item => item.Sessions)
            .AsSplitQuery();

    private Task<bool> IsDepartmentStoppedAsync(long departmentId, DateTimeOffset now,
        CancellationToken cancellationToken) => _dbContext.DepartmentClosures.AsNoTracking()
        .AnyAsync(item => item.DepartmentId == departmentId && item.CancelledAt == null &&
            item.StartAt <= now && now < item.EndAt, cancellationToken);
}
