using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Domain.Catalog;
using Clinic.Domain.Packages;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Packages;

public sealed class PackageCommandService : IPackageCommandService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PackageCommandService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<Result<PackageModel>> CreateAsync(long actorUserId, long departmentId,
        string name, decimal basePrice, int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<PackageServiceInput> services,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext, departmentId,
            cancellationToken);

        Department? department = await _dbContext.Departments.SingleOrDefaultAsync(
            item => item.Id == departmentId && !item.IsArchived, cancellationToken);
        if (department is null)
        {
            return Result.Failure<PackageModel>(PackageErrors.DepartmentNotFound);
        }

        if (await _dbContext.Packages.AnyAsync(item => item.Name == name, cancellationToken))
        {
            return Result.Failure<PackageModel>(PackageErrors.DuplicateName);
        }

        Result<IReadOnlyCollection<(Service Service, int SessionsIncluded, decimal UnitPrice)>>
            resolved = await ResolveServicesAsync(departmentId, services, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<PackageModel>(resolved.Error);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Package package = Package.Create(departmentId, name, basePrice, activationGraceDays,
            usageDurationDays, resolved.Value, actorUserId, now);
        _dbContext.Packages.Add(package);
        await _dbContext.SaveChangesAsync(cancellationToken);
        PackageInfrastructureSupport.AddAudit(_dbContext, actorUserId,
            PackageAuditActions.Created, package.Id, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        bool departmentStopped = await IsDepartmentStoppedAsync(
            package.DepartmentId, now, cancellationToken);
        PackageModel response = PackageInfrastructureSupport.Map(package, departmentStopped);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(response);
    });

    public Task<Result<PackageModel>> UpdateAsync(long actorUserId, long packageId, string name,
        decimal basePrice, int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<PackageServiceInput> services,
        byte[] expectedRowVersion, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        long? departmentId = await _dbContext.Packages.AsNoTracking()
            .Where(item => item.Id == packageId && !item.IsArchived)
            .Select(item => (long?)item.DepartmentId).SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return Result.Failure<PackageModel>(PackageErrors.NotFound);
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext, departmentId.Value,
            cancellationToken);
        await TransactionalResourceLock.AcquirePackageAsync(_dbContext, packageId,
            cancellationToken);

        Package? package = await FindTrackedAsync(packageId, cancellationToken);
        if (package is null)
        {
            return Result.Failure<PackageModel>(PackageErrors.NotFound);
        }

        if (!PackageInfrastructureSupport.MatchesVersion(package.RowVersion,
                expectedRowVersion))
        {
            return Result.Failure<PackageModel>(PackageErrors.ConcurrencyConflict);
        }

        if (await _dbContext.Packages.AnyAsync(
                item => item.Id != packageId && item.Name == name, cancellationToken))
        {
            return Result.Failure<PackageModel>(PackageErrors.DuplicateName);
        }

        Result<IReadOnlyCollection<(Service Service, int SessionsIncluded, decimal UnitPrice)>>
            resolved = await ResolveServicesAsync(departmentId.Value, services, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<PackageModel>(resolved.Error);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        package.Update(name, basePrice, activationGraceDays, usageDurationDays, resolved.Value,
            actorUserId, now);
        PackageInfrastructureSupport.AddAudit(_dbContext, actorUserId,
            PackageAuditActions.Updated, package.Id, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        bool departmentStopped = await IsDepartmentStoppedAsync(
            package.DepartmentId, now, cancellationToken);
        PackageModel response = PackageInfrastructureSupport.Map(package, departmentStopped);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(response);
    });

    public Task<Result<PackageModel>> SetActiveAsync(long actorUserId, long packageId,
        bool isActive, byte[] expectedRowVersion, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            long? departmentId = await _dbContext.Packages.AsNoTracking()
                .Where(item => item.Id == packageId && !item.IsArchived)
                .Select(item => (long?)item.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            if (departmentId is null)
            {
                return Result.Failure<PackageModel>(PackageErrors.NotFound);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
                departmentId.Value, cancellationToken);
            await TransactionalResourceLock.AcquirePackageAsync(_dbContext, packageId,
                cancellationToken);
            Package? package = await FindTrackedAsync(packageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<PackageModel>(PackageErrors.NotFound);
            }

            if (!PackageInfrastructureSupport.MatchesVersion(package.RowVersion,
                    expectedRowVersion))
            {
                return Result.Failure<PackageModel>(PackageErrors.ConcurrencyConflict);
            }

            if (isActive && package.Services.Where(item => item.IsActive).Any(item =>
                    item.Service.IsArchived || !item.Service.IsActive ||
                    item.Service.PricingMode != PricingMode.Fixed))
            {
                return Result.Failure<PackageModel>(PackageErrors.ServiceNotFound);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            package.SetActive(isActive, actorUserId, now);
            PackageInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                PackageAuditActions.ActivationChanged, package.Id, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            bool departmentStopped = await IsDepartmentStoppedAsync(
                package.DepartmentId, now, cancellationToken);
            PackageModel response = PackageInfrastructureSupport.Map(package, departmentStopped);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });

    public async Task<Result> ArchiveAsync(long actorUserId, long packageId,
        byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await ExecuteAsync(async () =>
        {
            long? departmentId = await _dbContext.Packages.AsNoTracking()
                .Where(item => item.Id == packageId && !item.IsArchived)
                .Select(item => (long?)item.DepartmentId).SingleOrDefaultAsync(cancellationToken);
            if (departmentId is null)
            {
                return Result.Failure<PackageModel>(PackageErrors.NotFound);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
                departmentId.Value, cancellationToken);
            await TransactionalResourceLock.AcquirePackageAsync(_dbContext, packageId,
                cancellationToken);
            Package? package = await FindTrackedAsync(packageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<PackageModel>(PackageErrors.NotFound);
            }

            if (!PackageInfrastructureSupport.MatchesVersion(package.RowVersion,
                    expectedRowVersion))
            {
                return Result.Failure<PackageModel>(PackageErrors.ConcurrencyConflict);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            package.Archive(actorUserId, now);
            PackageInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                PackageAuditActions.Archived, package.Id, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            PackageModel response = PackageInfrastructureSupport.Map(package, false);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }

    private async Task<Result<IReadOnlyCollection<(Service Service, int SessionsIncluded,
        decimal UnitPrice)>>> ResolveServicesAsync(long departmentId,
        IReadOnlyCollection<PackageServiceInput> inputs, CancellationToken cancellationToken)
    {
        long[] ids = inputs.Select(item => item.ServiceId).Order().ToArray();
        foreach (long serviceId in ids)
        {
            await TransactionalResourceLock.AcquireServiceAsync(_dbContext, serviceId,
                cancellationToken);
        }

        List<Service> services = await _dbContext.Services
            .Include(item => item.Specialization)
            .Where(item => ids.Contains(item.Id) && item.DepartmentId == departmentId &&
                           !item.IsArchived && item.IsActive &&
                           item.PricingMode == PricingMode.Fixed)
            .ToListAsync(cancellationToken);
        if (services.Count != ids.Length)
        {
            return Result.Failure<IReadOnlyCollection<(Service, int, decimal)>>(
                PackageErrors.ServiceNotFound);
        }

        return Result.Success<IReadOnlyCollection<(Service, int, decimal)>>(inputs.Select(input =>
            (services.Single(service => service.Id == input.ServiceId), input.SessionsIncluded,
                input.UnitPriceAtDefinition)).ToArray());
    }

    private Task<Package?> FindTrackedAsync(long packageId, CancellationToken cancellationToken) =>
        _dbContext.Packages.Include(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.Service)
                .ThenInclude(item => item.Specialization)
            .AsSplitQuery().SingleOrDefaultAsync(
                item => item.Id == packageId && !item.IsArchived, cancellationToken);

    private Task<bool> IsDepartmentStoppedAsync(long departmentId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return _dbContext.DepartmentClosures.AsNoTracking().AnyAsync(item =>
            item.DepartmentId == departmentId && item.CancelledAt == null &&
            item.StartAt <= now && now < item.EndAt, cancellationToken);
    }

    private async Task<Result<PackageModel>> ExecuteAsync(
        Func<Task<Result<PackageModel>>> operation)
    {
        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                return await operation();
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PackageModel>(
                PackageInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }
}
