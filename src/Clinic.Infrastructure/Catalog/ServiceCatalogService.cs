using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Common;
using Clinic.Domain.Catalog;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Catalog;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ServiceCatalogService(ClinicDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ServiceModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        long specializationId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        decimal unitPrice,
        CancellationToken cancellationToken)
    {
        Specialization? specialization = await _dbContext.Specializations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == specializationId && !item.IsArchived,
                cancellationToken);
        if (specialization is null)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.SpecializationNotFound);
        }

        if (specialization.DepartmentId != departmentId)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.Validation(
                "التخصص لا ينتمي إلى القسم المحدد."));
        }

        bool duplicate = await _dbContext.Services.AnyAsync(
            item => item.SpecializationId == specializationId && item.Name == name,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.DuplicateName);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Service service = Service.Create(
            departmentId,
            specializationId,
            name,
            serviceType,
            durationMinutes,
            pricingMode,
            unitPrice,
            actorUserId,
            now);

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                _dbContext.Services.Add(service);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.ServiceCreated,
                    nameof(Service),
                    service.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(CatalogMapper.Map(service));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<ServiceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<ServiceModel>> UpdateAsync(
        long actorUserId,
        long serviceId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        long? departmentId = await _dbContext.Services.AsNoTracking()
            .Where(item => item.Id == serviceId && !item.IsArchived)
            .Select(item => (long?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.ServiceNotFound);
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
                await TransactionalResourceLock.AcquireServiceAsync(
                    _dbContext, serviceId, cancellationToken);

                Service? service = await FindTrackedServiceAsync(serviceId, cancellationToken);
                if (service is null)
                {
                    return Result.Failure<ServiceModel>(CatalogErrors.ServiceNotFound);
                }

                if (!CatalogInfrastructureSupport.MatchesVersion(
                        service.RowVersion, expectedRowVersion))
                {
                    return Result.Failure<ServiceModel>(CatalogErrors.ConcurrencyConflict);
                }

                bool duplicate = await _dbContext.Services.AnyAsync(
                    item => item.Id != serviceId &&
                            item.SpecializationId == service.SpecializationId &&
                            item.Name == name,
                    cancellationToken);
                if (duplicate)
                {
                    return Result.Failure<ServiceModel>(CatalogErrors.DuplicateName);
                }

                service.UpdateDetails(name, serviceType, durationMinutes, pricingMode);
                DateTimeOffset now = _timeProvider.GetUtcNow();
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.ServiceUpdated,
                    nameof(Service),
                    service.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                ServiceModel response = CatalogMapper.Map(service);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(response);
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<ServiceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<ServiceModel>> ChangePriceAsync(
        long actorUserId,
        long serviceId,
        decimal unitPrice,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Service? service = await FindTrackedServiceAsync(serviceId, cancellationToken);
        if (service is null)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.ServiceNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(service.RowVersion, expectedRowVersion))
        {
            return Result.Failure<ServiceModel>(CatalogErrors.ConcurrencyConflict);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        service.ChangePrice(unitPrice, actorUserId, now);

        try
        {
            IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.ServicePriceChanged,
                    nameof(Service),
                    service.Id,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(CatalogMapper.Map(service));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<ServiceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<ServiceModel>> ReplaceDevicesAsync(
        long actorUserId,
        long serviceId,
        IReadOnlyCollection<ServiceDeviceInput> devices,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        Service? service = await FindTrackedServiceAsync(serviceId, cancellationToken);
        if (service is null)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.ServiceNotFound);
        }

        if (!CatalogInfrastructureSupport.MatchesVersion(service.RowVersion, expectedRowVersion))
        {
            return Result.Failure<ServiceModel>(CatalogErrors.ConcurrencyConflict);
        }

        long[] requestedIds = devices.Select(item => item.DeviceId).ToArray();
        List<Device> requestedDevices = await _dbContext.Devices
            .Where(item => requestedIds.Contains(item.Id) && !item.IsArchived && item.IsActive)
            .ToListAsync(cancellationToken);
        if (requestedDevices.Count != requestedIds.Length)
        {
            return Result.Failure<ServiceModel>(CatalogErrors.DeviceNotFound);
        }

        if (requestedDevices.Any(item => item.DepartmentId != service.DepartmentId))
        {
            return Result.Failure<ServiceModel>(CatalogErrors.Validation(
                "يجب أن تنتمي كل الأجهزة إلى قسم الخدمة."));
        }

        IReadOnlyCollection<(Device Device, bool IsRequired)> assignments = devices
            .Select(input => (
                requestedDevices.Single(device => device.Id == input.DeviceId),
                input.IsRequired))
            .ToArray();
        service.ReplaceDeviceAssignments(assignments);

        _dbContext.Entry(service).Property(item => item.IsActive).IsModified = true;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        CatalogInfrastructureSupport.AddAudit(
            _dbContext,
            actorUserId,
            CatalogAuditActions.ServiceDevicesReplaced,
            nameof(Service),
            service.Id,
            now);

        return await SaveServiceAsync(service, cancellationToken);
    }

    public async Task<Result> ArchiveAsync(
        long actorUserId,
        long serviceId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        long? departmentId = await _dbContext.Services.AsNoTracking()
            .Where(item => item.Id == serviceId && !item.IsArchived)
            .Select(item => (long?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return Result.Failure(CatalogErrors.ServiceNotFound);
        }

        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                await TransactionalResourceLock.AcquireDepartmentAsync(
                    _dbContext, departmentId.Value, cancellationToken);
                await TransactionalResourceLock.AcquireServiceAsync(
                    _dbContext, serviceId, cancellationToken);

                Service? service = await FindTrackedServiceAsync(serviceId, cancellationToken);
                if (service is null)
                {
                    return Result.Failure(CatalogErrors.ServiceNotFound);
                }

                if (!CatalogInfrastructureSupport.MatchesVersion(
                        service.RowVersion,
                        expectedRowVersion))
                {
                    return Result.Failure(CatalogErrors.ConcurrencyConflict);
                }

                bool assignedToDoctor = await _dbContext.DoctorServices.AnyAsync(
                    item => item.ServiceId == serviceId && item.IsActive,
                    cancellationToken);
                if (assignedToDoctor)
                {
                    return Result.Failure(CatalogErrors.DependencyConflict(
                        "يجب إزالة الخدمة من الأطباء المرتبطين بها أولًا."));
                }

                bool assignedToActivePackage = await _dbContext.PackageServices.AnyAsync(
                    item => item.ServiceId == serviceId && item.IsActive &&
                            item.Package.IsActive && !item.Package.IsArchived,
                    cancellationToken);
                if (assignedToActivePackage)
                {
                    return Result.Failure(CatalogErrors.DependencyConflict(
                        "يجب إزالة الخدمة من الباقات الفعالة المرتبطة بها أولًا."));
                }

                service.Archive();
                DateTimeOffset now = _timeProvider.GetUtcNow();
                CatalogInfrastructureSupport.AddAudit(
                    _dbContext,
                    actorUserId,
                    CatalogAuditActions.ServiceArchived,
                    nameof(Service),
                    service.Id,
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

    private Task<Service?> FindTrackedServiceAsync(
        long serviceId,
        CancellationToken cancellationToken) =>
        _dbContext.Services
            .Include(item => item.PriceHistory)
            .Include(item => item.DeviceAssignments)
                .ThenInclude(item => item.Device)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == serviceId && !item.IsArchived,
                cancellationToken);

    private async Task<Result<ServiceModel>> SaveServiceAsync(
        Service service,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(CatalogMapper.Map(service));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<ServiceModel>(
                CatalogInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }
}
