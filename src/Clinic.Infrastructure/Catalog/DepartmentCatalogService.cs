using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Common;
using Clinic.Domain.Catalog;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Catalog;

public sealed class DepartmentCatalogService : IDepartmentCatalogService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DepartmentCatalogService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DepartmentModel>> CreateAsync(
        long actorUserId,
        string name,
        string? description,
        string roomName,
        CancellationToken cancellationToken)
    {
        bool duplicate = await _dbContext.Departments.AnyAsync(
            item => item.Name == name,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<DepartmentModel>(CatalogErrors.DuplicateName);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Department department = Department.Create(name, description, roomName, now);

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                _dbContext.Departments.Add(department);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.DepartmentCreated,
                    nameof(Department),
                    department.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(CatalogMapper.Map(department, DepartmentStatus.Active));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DepartmentModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<DepartmentModel>> UpdateAsync(
        long actorUserId,
        long departmentId,
        string name,
        string? description,
        string roomName,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Department? department = await _dbContext.Departments
            .Include(item => item.Room)
            .SingleOrDefaultAsync(
                item => item.Id == departmentId && !item.IsArchived,
                cancellationToken);
        if (department is null)
        {
            return Result.Failure<DepartmentModel>(CatalogErrors.DepartmentNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(
                department.RowVersion,
                expectedRowVersion))
        {
            return Result.Failure<DepartmentModel>(CatalogErrors.ConcurrencyConflict);
        }

        bool duplicate = await _dbContext.Departments.AnyAsync(
            item => item.Id != departmentId && item.Name == name,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<DepartmentModel>(CatalogErrors.DuplicateName);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        department.Update(name, description, roomName, now);
        CatalogInfrastructureSupport.AddAudit(
            _dbContext,
            actorUserId,
            CatalogAuditActions.DepartmentUpdated,
            nameof(Department),
            department.Id,
            now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            bool stopped = await _dbContext.DepartmentClosures.AsNoTracking().AnyAsync(
                item => item.DepartmentId == department.Id && item.CancelledAt == null &&
                        item.StartAt <= now && now < item.EndAt,
                cancellationToken);
            return Result.Success(CatalogMapper.Map(
                department,
                stopped ? DepartmentStatus.Stopped : DepartmentStatus.Active));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DepartmentModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchiveAsync(
        long actorUserId,
        long departmentId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId, cancellationToken);
                Department? department = await _dbContext.Departments
                    .Include(item => item.Room)
                    .SingleOrDefaultAsync(
                        item => item.Id == departmentId && !item.IsArchived,
                        cancellationToken);
                if (department is null)
                {
                    return Result.Failure(CatalogErrors.DepartmentNotFound);
                }

                if (!CatalogInfrastructureSupport.MatchesVersion(
                        department.RowVersion,
                        expectedRowVersion))
                {
                    return Result.Failure(CatalogErrors.ConcurrencyConflict);
                }

                bool hasActiveChildren = await _dbContext.Specializations.AnyAsync(
                    item => item.DepartmentId == departmentId && !item.IsArchived,
                    cancellationToken) || await _dbContext.Devices.AnyAsync(
                    item => item.DepartmentId == departmentId && !item.IsArchived,
                    cancellationToken);
                bool hasDoctorsOrClosures = await _dbContext.Doctors.AnyAsync(
                    item => item.DepartmentId == departmentId && !item.IsArchived,
                    cancellationToken) || await _dbContext.DepartmentClosures.AnyAsync(
                    item => item.DepartmentId == departmentId && item.CancelledAt == null &&
                            item.EndAt > now,
                    cancellationToken);
                bool hasPackages = await _dbContext.Packages.AnyAsync(
                    item => item.DepartmentId == departmentId && !item.IsArchived,
                    cancellationToken);
                if (hasActiveChildren || hasDoctorsOrClosures || hasPackages)
                {
                    return Result.Failure(CatalogErrors.DependencyConflict(
                        "يجب أرشفة تخصصات وأجهزة وأطباء وباقات القسم وإلغاء فترات الإيقاف الحالية أو القادمة أولًا."));
                }

                department.Archive(now);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.DepartmentArchived,
                    nameof(Department),
                    department.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }
}
