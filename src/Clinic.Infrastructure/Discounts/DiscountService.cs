using System.Globalization;
using System.Text.Json;
using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Common;
using Clinic.Application.Features.Discounts;
using Clinic.Domain.Auditing;
using Clinic.Domain.Common;
using Clinic.Domain.Discounts;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Discounts;

public sealed class DiscountService : IDiscountService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DiscountService(ClinicDbContext dbContext, TimeProvider timeProvider) =>
        (_dbContext, _timeProvider) = (dbContext, timeProvider);

    public Task<Result<DiscountModel>> CreateAsync(long actorUserId,
        DiscountDefinition definition, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDiscountScheduleWriteAsync(_dbContext,
                cancellationToken);
            Result<TargetResolution> targets = await ResolveTargetsAsync(definition,
                cancellationToken);
            if (targets.IsFailure)
            {
                return Result.Failure<DiscountModel>(targets.Error);
            }
            if (await OverlapsAsync(null, definition, targets.Value, cancellationToken))
            {
                return Result.Failure<DiscountModel>(DiscountErrors.Overlap);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            Discount discount = Discount.Create(definition.Name, definition.Type,
                definition.Value, definition.AppliesTo, definition.ScopeMode,
                definition.StartAt, definition.EndAt, definition.Targets.DepartmentIds,
                definition.Targets.ServiceIds, definition.Targets.PackageIds,
                actorUserId, now);
            _dbContext.Discounts.Add(discount);
            await _dbContext.SaveChangesAsync(cancellationToken);
            AddAudit(actorUserId, "discounts.discount.created", discount, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            Discount persisted = await FullQuery(tracked: false).SingleAsync(item =>
                item.Id == discount.Id, cancellationToken);
            DiscountModel response = Map(persisted, now);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });

    public Task<Result<DiscountModel>> UpdateAsync(long actorUserId, long discountId,
        DiscountDefinition definition, byte[] expectedRowVersion,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        await TransactionalResourceLock.AcquireDiscountScheduleWriteAsync(_dbContext,
            cancellationToken);
        Discount? discount = await FullQuery(tracked: true).SingleOrDefaultAsync(item =>
            item.Id == discountId && !item.IsArchived, cancellationToken);
        if (discount is null)
        {
            return Result.Failure<DiscountModel>(DiscountErrors.NotFound);
        }
        if (!discount.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
        {
            return Result.Failure<DiscountModel>(DiscountErrors.ConcurrencyConflict);
        }

        Result<TargetResolution> targets = await ResolveTargetsAsync(definition,
            cancellationToken);
        if (targets.IsFailure)
        {
            return Result.Failure<DiscountModel>(targets.Error);
        }
        if (discount.IsActive && await OverlapsAsync(discountId, definition,
            targets.Value, cancellationToken))
        {
            return Result.Failure<DiscountModel>(DiscountErrors.Overlap);
        }

        _dbContext.RemoveRange(discount.Departments);
        _dbContext.RemoveRange(discount.Services);
        _dbContext.RemoveRange(discount.Packages);
        await _dbContext.SaveChangesAsync(cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        discount.Update(definition.Name, definition.Type, definition.Value,
            definition.AppliesTo, definition.ScopeMode, definition.StartAt,
            definition.EndAt, definition.Targets.DepartmentIds,
            definition.Targets.ServiceIds, definition.Targets.PackageIds,
            actorUserId, now);
        AddAudit(actorUserId, "discounts.discount.updated", discount, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        Discount persisted = await FullQuery(tracked: false).SingleAsync(item =>
            item.Id == discount.Id, cancellationToken);
        DiscountModel response = Map(persisted, now);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(response);
    });

    public Task<Result<DiscountModel>> SetActiveAsync(long actorUserId, long discountId,
        bool isActive, byte[] expectedRowVersion, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDiscountScheduleWriteAsync(_dbContext,
                cancellationToken);
            Discount? discount = await FullQuery(tracked: true).SingleOrDefaultAsync(item =>
                item.Id == discountId && !item.IsArchived, cancellationToken);
            if (discount is null)
            {
                return Result.Failure<DiscountModel>(DiscountErrors.NotFound);
            }
            if (!discount.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
            {
                return Result.Failure<DiscountModel>(DiscountErrors.ConcurrencyConflict);
            }

            if (isActive && !discount.IsActive)
            {
                DiscountDefinition definition = Definition(discount);
                TargetResolution targets = Resolution(discount);
                if (await OverlapsAsync(discountId, definition, targets, cancellationToken))
                {
                    return Result.Failure<DiscountModel>(DiscountErrors.Overlap);
                }
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            discount.SetActive(isActive, actorUserId, now);
            AddAudit(actorUserId, "discounts.discount.activation_changed", discount, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            DiscountModel response = Map(discount, now);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });

    public async Task<Result> ArchiveAsync(long actorUserId, long discountId,
        byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        Result<DiscountModel> result = await ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            await TransactionalResourceLock.AcquireDiscountScheduleWriteAsync(_dbContext,
                cancellationToken);
            Discount? discount = await FullQuery(tracked: true).SingleOrDefaultAsync(item =>
                item.Id == discountId && !item.IsArchived, cancellationToken);
            if (discount is null)
            {
                return Result.Failure<DiscountModel>(DiscountErrors.NotFound);
            }
            if (!discount.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
            {
                return Result.Failure<DiscountModel>(DiscountErrors.ConcurrencyConflict);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            discount.Archive(actorUserId, now);
            AddAudit(actorUserId, "discounts.discount.archived", discount, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            DiscountModel response = Map(discount, now);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        });
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }

    public async Task<Result<DiscountModel>> GetAsync(long discountId,
        CancellationToken cancellationToken)
    {
        Discount? discount = await FullQuery(tracked: false).SingleOrDefaultAsync(item =>
            item.Id == discountId, cancellationToken);
        return discount is null ? Result.Failure<DiscountModel>(DiscountErrors.NotFound)
            : Result.Success(Map(discount, _timeProvider.GetUtcNow()));
    }

    public async Task<Result<DiscountPage>> SearchAsync(DiscountFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<Discount> query = _dbContext.Discounts.AsNoTracking().Where(item =>
            filter.IncludeArchived || !item.IsArchived);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim();
            query = query.Where(item => item.Name.Contains(search));
        }
        if (filter.Type.HasValue) query = query.Where(item => item.Type == filter.Type.Value);
        if (filter.AppliesTo.HasValue)
            query = query.Where(item => item.AppliesTo == filter.AppliesTo.Value);
        if (filter.IsActive.HasValue)
            query = query.Where(item => item.IsActive == filter.IsActive.Value);
        if (filter.EffectiveAt.HasValue)
        {
            DateTimeOffset at = filter.EffectiveAt.Value;
            query = query.Where(item => item.IsActive && !item.IsArchived &&
                item.StartAt <= at && at < item.EndAt);
        }
        if (filter.DepartmentId.HasValue)
        {
            long id = filter.DepartmentId.Value;
            query = query.Where(item => item.ScopeMode == DiscountScopeMode.All ||
                item.Departments.Any(target => target.DepartmentId == id) ||
                item.Services.Any(target => target.Service.DepartmentId == id) ||
                item.Packages.Any(target => target.Package.DepartmentId == id));
        }

        int total = await query.CountAsync(cancellationToken);
        List<Discount> discounts = await FullQuery(query, tracked: false)
            .OrderByDescending(item => item.StartAt).ThenBy(item => item.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync(cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return Result.Success(new DiscountPage(discounts.Select(item => Map(item, now)).ToArray(),
            filter.PageNumber, filter.PageSize, total));
    }

    private async Task<Result<TargetResolution>> ResolveTargetsAsync(
        DiscountDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.ScopeMode == DiscountScopeMode.All)
        {
            return Result.Success(TargetResolution.Empty);
        }

        long[] departmentIds = definition.Targets.DepartmentIds.ToArray();
        long[] serviceIds = definition.Targets.ServiceIds.ToArray();
        long[] packageIds = definition.Targets.PackageIds.ToArray();
        Dictionary<long, long> services = await _dbContext.Services.AsNoTracking()
            .Where(item => serviceIds.Contains(item.Id) && !item.IsArchived)
            .ToDictionaryAsync(item => item.Id, item => item.DepartmentId, cancellationToken);
        Dictionary<long, long> packages = await _dbContext.Packages.AsNoTracking()
            .Where(item => packageIds.Contains(item.Id) && !item.IsArchived)
            .ToDictionaryAsync(item => item.Id, item => item.DepartmentId, cancellationToken);
        int departments = await _dbContext.Departments.AsNoTracking().CountAsync(item =>
            departmentIds.Contains(item.Id) && !item.IsArchived, cancellationToken);
        if (departments != departmentIds.Length || services.Count != serviceIds.Length ||
            packages.Count != packageIds.Length)
        {
            return Result.Failure<TargetResolution>(DiscountErrors.TargetNotFound);
        }

        return Result.Success(new TargetResolution(departmentIds.ToHashSet(), services, packages));
    }

    private async Task<bool> OverlapsAsync(long? excludedId, DiscountDefinition definition,
        TargetResolution targets, CancellationToken cancellationToken)
    {
        List<Discount> candidates = await FullQuery(tracked: false).Where(item =>
            item.IsActive && !item.IsArchived && (!excludedId.HasValue || item.Id != excludedId) &&
            item.StartAt < definition.EndAt && definition.StartAt < item.EndAt)
            .ToListAsync(cancellationToken);
        return candidates.Any(existing => AppliesOverlap(existing.AppliesTo,
                definition.AppliesTo) && CoverageOverlaps(existing, definition, targets));
    }

    private static bool AppliesOverlap(DiscountAppliesTo left, DiscountAppliesTo right) =>
        left == DiscountAppliesTo.Both || right == DiscountAppliesTo.Both || left == right;

    private static bool CoverageOverlaps(Discount existing, DiscountDefinition proposed,
        TargetResolution targets)
    {
        if (existing.ScopeMode == DiscountScopeMode.All ||
            proposed.ScopeMode == DiscountScopeMode.All)
        {
            return true;
        }

        TargetResolution old = Resolution(existing);
        bool bookings = existing.AppliesToBookings &&
            (proposed.AppliesTo is DiscountAppliesTo.Bookings or DiscountAppliesTo.Both) &&
            BranchOverlaps(old.Departments, old.Services, targets.Departments, targets.Services);
        bool packages = existing.AppliesToPackages &&
            (proposed.AppliesTo is DiscountAppliesTo.Packages or DiscountAppliesTo.Both) &&
            BranchOverlaps(old.Departments, old.Packages, targets.Departments, targets.Packages);
        return bookings || packages;
    }

    private static bool BranchOverlaps(HashSet<long> leftDepartments,
        IReadOnlyDictionary<long, long> leftItems, HashSet<long> rightDepartments,
        IReadOnlyDictionary<long, long> rightItems) =>
        leftDepartments.Overlaps(rightDepartments) ||
        leftItems.Keys.Intersect(rightItems.Keys).Any() ||
        leftItems.Values.Any(rightDepartments.Contains) ||
        rightItems.Values.Any(leftDepartments.Contains);

    private IQueryable<Discount> FullQuery(bool tracked) => FullQuery(_dbContext.Discounts,
        tracked);

    private static IQueryable<Discount> FullQuery(IQueryable<Discount> source, bool tracked) =>
        (tracked ? source : source.AsNoTracking())
            .Include(item => item.Departments).ThenInclude(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.Service)
            .Include(item => item.Packages).ThenInclude(item => item.Package)
            .AsSplitQuery();

    private static DiscountModel Map(Discount discount, DateTimeOffset now) => new(
        discount.Id, discount.Name, discount.Type, discount.Value, discount.AppliesTo,
        discount.ScopeMode, discount.StartAt, discount.EndAt, discount.IsActive,
        discount.IsArchived, discount.IsEffectiveAt(now), discount.Departments
            .OrderBy(item => item.Department.Name).Select(item =>
                new DiscountTargetModel(item.DepartmentId, item.Department.Name)).ToArray(),
        discount.Services.OrderBy(item => item.Service.Name).Select(item =>
            new DiscountTargetModel(item.ServiceId, item.Service.Name)).ToArray(),
        discount.Packages.OrderBy(item => item.Package.Name).Select(item =>
            new DiscountTargetModel(item.PackageId, item.Package.Name)).ToArray(),
        discount.CreatedAt, discount.UpdatedAt, Convert.ToBase64String(discount.RowVersion));

    private void AddAudit(long actorUserId, string action, Discount discount,
        DateTimeOffset occurredAt) => _dbContext.AuditLogs.Add(AuditLog.CreateForUser(
            actorUserId, action, nameof(Discount),
            discount.Id.ToString(CultureInfo.InvariantCulture), occurredAt,
            dataJson: JsonSerializer.Serialize(new { discount.Name, discount.Type,
                discount.Value, discount.AppliesTo, discount.ScopeMode, discount.StartAt,
                discount.EndAt, discount.IsActive, discount.IsArchived })));

    private static DiscountDefinition Definition(Discount discount) => new(discount.Name,
        discount.Type, discount.Value, discount.AppliesTo, discount.ScopeMode,
        discount.StartAt, discount.EndAt, new DiscountTargetInput(
            discount.Departments.Select(item => item.DepartmentId).ToArray(),
            discount.Services.Select(item => item.ServiceId).ToArray(),
            discount.Packages.Select(item => item.PackageId).ToArray()));

    private static TargetResolution Resolution(Discount discount) => new(
        discount.Departments.Select(item => item.DepartmentId).ToHashSet(),
        discount.Services.ToDictionary(item => item.ServiceId,
            item => item.Service.DepartmentId),
        discount.Packages.ToDictionary(item => item.PackageId,
            item => item.Package.DepartmentId));

    private async Task<Result<DiscountModel>> ExecuteAsync(
        Func<Task<Result<DiscountModel>>> operation)
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
        catch (DomainException exception)
        {
            return Result.Failure<DiscountModel>(DiscountErrors.Validation(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<DiscountModel>(DiscountErrors.ConcurrencyConflict);
        }
    }

    private sealed record TargetResolution(HashSet<long> Departments,
        IReadOnlyDictionary<long, long> Services, IReadOnlyDictionary<long, long> Packages)
    {
        public static readonly TargetResolution Empty = new([], new Dictionary<long, long>(),
            new Dictionary<long, long>());
    }
}
