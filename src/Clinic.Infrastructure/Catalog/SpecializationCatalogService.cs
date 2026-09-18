namespace Clinic.Infrastructure.Catalog;

public sealed class SpecializationCatalogService : ISpecializationCatalogService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SpecializationCatalogService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<SpecializationModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        string name,
        CancellationToken cancellationToken)
    {
        Specialization specialization = Specialization.Create(departmentId, name);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId, cancellationToken);
                bool departmentExists = await _dbContext.Departments.AnyAsync(
                    item => item.Id == departmentId && !item.IsArchived,
                    cancellationToken);
                if (!departmentExists)
                {
                    return Result.Failure<SpecializationModel>(
                        CatalogErrors.DepartmentNotFound);
                }

                bool duplicate = await _dbContext.Specializations.AnyAsync(
                    item => item.DepartmentId == departmentId && item.Name == name,
                    cancellationToken);
                if (duplicate)
                {
                    return Result.Failure<SpecializationModel>(CatalogErrors.DuplicateName);
                }

                _dbContext.Specializations.Add(specialization);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.SpecializationCreated,
                    nameof(Specialization),
                    specialization.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(CatalogMapper.Map(specialization));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<SpecializationModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<SpecializationModel>> UpdateAsync(
        long actorUserId,
        long specializationId,
        string name,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        long? departmentId = await _dbContext.Specializations.AsNoTracking()
            .Where(item => item.Id == specializationId && !item.IsArchived)
            .Select(item => (long?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return Result.Failure<SpecializationModel>(CatalogErrors.SpecializationNotFound);
        }

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId.Value, cancellationToken);

                Specialization? specialization = await _dbContext.Specializations
                    .SingleOrDefaultAsync(
                        item => item.Id == specializationId && !item.IsArchived,
                        cancellationToken);
                if (specialization is null)
                {
                    return Result.Failure<SpecializationModel>(
                        CatalogErrors.SpecializationNotFound);
                }

                if (!CatalogInfrastructureSupport.MatchesVersion(
                        specialization.RowVersion, expectedRowVersion))
                {
                    return Result.Failure<SpecializationModel>(
                        CatalogErrors.ConcurrencyConflict);
                }

                bool duplicate = await _dbContext.Specializations.AnyAsync(
                    item => item.Id != specializationId &&
                            item.DepartmentId == specialization.DepartmentId &&
                            item.Name == name,
                    cancellationToken);
                if (duplicate)
                {
                    return Result.Failure<SpecializationModel>(CatalogErrors.DuplicateName);
                }

                specialization.Rename(name);
                DateTimeOffset now = _timeProvider.GetUtcNow();
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.SpecializationUpdated,
                    nameof(Specialization),
                    specialization.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                SpecializationModel response = CatalogMapper.Map(specialization);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(response);
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<SpecializationModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchiveAsync(
        long actorUserId,
        long specializationId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Specialization? specialization = await _dbContext.Specializations
            .SingleOrDefaultAsync(
                item => item.Id == specializationId && !item.IsArchived,
                cancellationToken);
        if (specialization is null)
        {
            return Result.Failure(CatalogErrors.SpecializationNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(
                specialization.RowVersion,
                expectedRowVersion))
        {
            return Result.Failure(CatalogErrors.ConcurrencyConflict);
        }

        bool hasActiveServices = await _dbContext.Services.AnyAsync(
            item => item.SpecializationId == specializationId && !item.IsArchived,
            cancellationToken);
        if (hasActiveServices)
        {
            return Result.Failure(CatalogErrors.DependencyConflict(
                "يجب أرشفة خدمات التخصص أولًا."));
        }

        specialization.Archive();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CatalogInfrastructureSupport.AddAudit(
            _dbContext,
            actorUserId,
            CatalogAuditActions.SpecializationArchived,
            nameof(Specialization),
            specialization.Id,
            now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure(CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }
}
