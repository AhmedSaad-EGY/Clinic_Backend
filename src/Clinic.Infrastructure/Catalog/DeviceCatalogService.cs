namespace Clinic.Infrastructure.Catalog;

public sealed class DeviceCatalogService : IDeviceCatalogService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DeviceCatalogService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DeviceModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        string name,
        string? identifier,
        CancellationToken cancellationToken)
    {
        if (identifier is not null && await IdentifierExistsAsync(identifier, null, cancellationToken))
        {
            return Result.Failure<DeviceModel>(CatalogErrors.DuplicateIdentifier);
        }

        Device device = Device.Create(departmentId, name, identifier);
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
                    return Result.Failure<DeviceModel>(CatalogErrors.DepartmentNotFound);
                }

                _dbContext.Devices.Add(device);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.DeviceCreated,
                    nameof(Device),
                    device.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(CatalogMapper.Map(device));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DeviceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<DeviceModel>> UpdateAsync(
        long actorUserId,
        long deviceId,
        string name,
        string? identifier,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Device? device = await _dbContext.Devices.SingleOrDefaultAsync(
            item => item.Id == deviceId && !item.IsArchived,
            cancellationToken);
        if (device is null)
        {
            return Result.Failure<DeviceModel>(CatalogErrors.DeviceNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(device.RowVersion, expectedRowVersion))
        {
            return Result.Failure<DeviceModel>(CatalogErrors.ConcurrencyConflict);
        }

        if (identifier is not null &&
            await IdentifierExistsAsync(identifier, deviceId, cancellationToken))
        {
            return Result.Failure<DeviceModel>(CatalogErrors.DuplicateIdentifier);
        }

        device.Update(name, identifier);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CatalogInfrastructureSupport.AddAudit(
            _dbContext,
            actorUserId,
            CatalogAuditActions.DeviceUpdated,
            nameof(Device),
            device.Id,
            now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(CatalogMapper.Map(device));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<DeviceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result> ArchiveAsync(
        long actorUserId,
        long deviceId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Device? device = await _dbContext.Devices.SingleOrDefaultAsync(
            item => item.Id == deviceId && !item.IsArchived,
            cancellationToken);
        if (device is null)
        {
            return Result.Failure(CatalogErrors.DeviceNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(device.RowVersion, expectedRowVersion))
        {
            return Result.Failure(CatalogErrors.ConcurrencyConflict);
        }

        bool isAssigned = await _dbContext.ServiceDevices.AnyAsync(
            item => item.DeviceId == deviceId && item.IsActive,
            cancellationToken);
        if (isAssigned)
        {
            return Result.Failure(CatalogErrors.DependencyConflict(
                "يجب إزالة الجهاز من الخدمات المرتبطة به أولًا."));
        }

        device.Archive();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CatalogInfrastructureSupport.AddAudit(
            _dbContext,
            actorUserId,
            CatalogAuditActions.DeviceArchived,
            nameof(Device),
            device.Id,
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

    private Task<bool> IdentifierExistsAsync(
        string identifier,
        long? excludedId,
        CancellationToken cancellationToken) =>
        _dbContext.Devices.AnyAsync(
            item => item.Identifier == identifier &&
                    !item.IsArchived &&
                    (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);
}
