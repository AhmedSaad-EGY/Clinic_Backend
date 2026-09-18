namespace Clinic.Infrastructure.Discounts;

public sealed class DiscountResolver(ClinicDbContext dbContext)
{
    public async Task ApplyToAppointmentAsync(Appointment appointment,
        DiscountOverrideInput? discountOverride, long actorUserId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (appointment.PatientPackageId.HasValue)
        {
            return;
        }

        if (discountOverride?.Mode == DiscountOverrideMode.Exclude)
        {
            appointment.ExcludeDiscount(actorUserId, discountOverride.Reason);
            appointment.FinalizePricing();
            return;
        }

        long[] serviceIds = appointment.Services.Select(item => item.ServiceId)
            .Distinct().ToArray();
        IQueryable<Discount> query = BookingQuery().Where(item =>
            item.AppliesTo == DiscountAppliesTo.Bookings ||
            item.AppliesTo == DiscountAppliesTo.Both).Where(item =>
                item.ScopeMode == DiscountScopeMode.All ||
                item.Departments.Any(target =>
                    target.DepartmentId == appointment.DepartmentId) ||
                item.Services.Any(target => serviceIds.Contains(target.ServiceId)));
        if (discountOverride?.Mode == DiscountOverrideMode.Force)
        {
            query = query.Where(item => item.Id == discountOverride.DiscountId &&
                !item.IsArchived);
        }
        else
        {
            query = query.Where(item => item.IsActive && !item.IsArchived &&
                item.StartAt <= now && now < item.EndAt);
        }

        List<Discount> discounts = await query.ToListAsync(cancellationToken);
        if (discountOverride?.Mode == DiscountOverrideMode.Force && discounts.Count != 1)
        {
            throw new DomainException("الخصم المحدد للاستثناء غير موجود أو لا يخص الحجوزات.");
        }

        bool applied = false;
        foreach (Clinic.Domain.Appointments.AppointmentService line in appointment.Services)
        {
            Discount? discount = discounts.FirstOrDefault(item => MatchesBooking(item,
                appointment.DepartmentId, line.ServiceId));
            if (discount is null)
            {
                continue;
            }

            appointment.ApplyDiscount(line.SequenceNumber, discount.Id,
                discount.Calculate(line.GrossAmount), discountOverride?.Mode,
                discountOverride is null ? null : actorUserId,
                discountOverride?.Reason);
            applied = true;
        }

        if (discountOverride is not null && !applied)
        {
            throw new DomainException("الخصم المحدد لا ينطبق على خدمات هذا الحجز.");
        }

        appointment.FinalizePricing();
    }

    public async Task<Discount?> ResolveForPackageAsync(long departmentId, long packageId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await PackageQuery().Where(item => item.IsActive &&
            !item.IsArchived && item.StartAt <= now && now < item.EndAt &&
            (item.AppliesTo == DiscountAppliesTo.Packages ||
             item.AppliesTo == DiscountAppliesTo.Both) &&
            (item.ScopeMode == DiscountScopeMode.All ||
             item.Departments.Any(target => target.DepartmentId == departmentId) ||
             item.Packages.Any(target => target.PackageId == packageId)))
            .OrderBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, DiscountQuote>> QuotePackagesAsync(
        IReadOnlyCollection<Package> packages, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
        {
            return new Dictionary<long, DiscountQuote>();
        }

        long[] departmentIds = packages.Select(item => item.DepartmentId).Distinct().ToArray();
        long[] packageIds = packages.Select(item => item.Id).ToArray();
        List<Discount> discounts = await PackageQuery().AsNoTracking().Where(item =>
            item.IsActive && !item.IsArchived && item.StartAt <= now && now < item.EndAt &&
            (item.AppliesTo == DiscountAppliesTo.Packages ||
             item.AppliesTo == DiscountAppliesTo.Both) &&
            (item.ScopeMode == DiscountScopeMode.All ||
             item.Departments.Any(target => departmentIds.Contains(target.DepartmentId)) ||
             item.Packages.Any(target => packageIds.Contains(target.PackageId))))
            .ToListAsync(cancellationToken);
        return packages.Select(package => (Package: package, Discount: discounts
                .FirstOrDefault(item => MatchesPackage(item, package.DepartmentId, package.Id))))
            .Where(item => item.Discount is not null)
            .ToDictionary(item => item.Package.Id, item => new DiscountQuote(
                item.Discount!.Id, item.Discount.Calculate(item.Package.BasePrice)));
    }

    private IQueryable<Discount> BookingQuery() => dbContext.Discounts
        .Include(item => item.Departments)
        .Include(item => item.Services)
        .AsSplitQuery();

    private IQueryable<Discount> PackageQuery() => dbContext.Discounts
        .Include(item => item.Departments)
        .Include(item => item.Packages)
        .AsSplitQuery();

    private static bool MatchesBooking(Discount discount, long departmentId,
        long serviceId) => discount.ScopeMode == DiscountScopeMode.All ||
        discount.Departments.Any(item => item.DepartmentId == departmentId) ||
        discount.Services.Any(item => item.ServiceId == serviceId);

    private static bool MatchesPackage(Discount discount, long departmentId,
        long packageId) => discount.ScopeMode == DiscountScopeMode.All ||
        discount.Departments.Any(item => item.DepartmentId == departmentId) ||
        discount.Packages.Any(item => item.PackageId == packageId);
}

public sealed record DiscountQuote(long DiscountId, decimal Amount);
