namespace Clinic.Domain.Discounts;

public sealed class Discount : AggregateRoot
{
    public const decimal MaximumMoney = 9999999999999999.99m;
    private readonly List<DiscountDepartment> _departments = [];
    private readonly List<DiscountService> _services = [];
    private readonly List<DiscountPackage> _packages = [];

    private Discount() { }

    private Discount(string name, DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt,
        IReadOnlyCollection<long> departmentIds, IReadOnlyCollection<long> serviceIds,
        IReadOnlyCollection<long> packageIds, long actorUserId, DateTimeOffset createdAt)
    {
        SetDetails(name, type, value, appliesTo, scopeMode, startAt, endAt,
            departmentIds, serviceIds, packageIds);
        CreatedByAdminUserId = ValidateId(actorUserId, "الأدمن");
        CreatedAt = NormalizeTimestamp(createdAt);
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public DiscountType Type { get; private set; }
    public decimal Value { get; private set; }
    public DiscountAppliesTo AppliesTo { get; private set; }
    public DiscountScopeMode ScopeMode { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public long CreatedByAdminUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? UpdatedByAdminUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<DiscountDepartment> Departments => _departments;
    public IReadOnlyCollection<DiscountService> Services => _services;
    public IReadOnlyCollection<DiscountPackage> Packages => _packages;

    public static Discount Create(string name, DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt,
        IReadOnlyCollection<long> departmentIds, IReadOnlyCollection<long> serviceIds,
        IReadOnlyCollection<long> packageIds, long actorUserId, DateTimeOffset createdAt) =>
        new(name, type, value, appliesTo, scopeMode, startAt, endAt,
            departmentIds, serviceIds, packageIds, actorUserId, createdAt);

    public void Update(string name, DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt,
        IReadOnlyCollection<long> departmentIds, IReadOnlyCollection<long> serviceIds,
        IReadOnlyCollection<long> packageIds, long actorUserId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        SetDetails(name, type, value, appliesTo, scopeMode, startAt, endAt,
            departmentIds, serviceIds, packageIds);
        Touch(actorUserId, updatedAt);
    }

    public void SetActive(bool isActive, long actorUserId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        IsActive = isActive;
        Touch(actorUserId, updatedAt);
    }

    public void Archive(long actorUserId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        IsActive = false;
        IsArchived = true;
        Touch(actorUserId, updatedAt);
    }

    public bool AppliesToBookings => AppliesTo is DiscountAppliesTo.Bookings or
        DiscountAppliesTo.Both;

    public bool AppliesToPackages => AppliesTo is DiscountAppliesTo.Packages or
        DiscountAppliesTo.Both;

    public bool IsEffectiveAt(DateTimeOffset timestamp) => IsActive && !IsArchived &&
        StartAt <= timestamp && timestamp < EndAt;

    public decimal Calculate(decimal grossAmount)
    {
        if (grossAmount < 0 || grossAmount > MaximumMoney)
        {
            throw new DomainException("قيمة السعر المطلوب خصمها غير صحيحة.");
        }

        decimal amount = Type == DiscountType.Percentage
            ? decimal.Round(grossAmount * Value / 100m, 2, MidpointRounding.AwayFromZero)
            : Value;
        return Math.Min(amount, grossAmount);
    }

    private void SetDetails(string name, DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt,
        IReadOnlyCollection<long> departmentIds, IReadOnlyCollection<long> serviceIds,
        IReadOnlyCollection<long> packageIds)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);
        ArgumentNullException.ThrowIfNull(serviceIds);
        ArgumentNullException.ThrowIfNull(packageIds);
        DateTimeOffset normalizedStartAt = NormalizeTimestamp(startAt);
        DateTimeOffset normalizedEndAt = NormalizeTimestamp(endAt);
        Name = ValidateName(name);
        ValidateDefinition(type, value, appliesTo, scopeMode,
            normalizedStartAt, normalizedEndAt,
            departmentIds, serviceIds, packageIds);
        Type = type;
        Value = value;
        AppliesTo = appliesTo;
        ScopeMode = scopeMode;
        StartAt = normalizedStartAt;
        EndAt = normalizedEndAt;
        _departments.Clear();
        _services.Clear();
        _packages.Clear();
        _departments.AddRange(departmentIds.Distinct().Select(id =>
            new DiscountDepartment(this, ValidateId(id, "القسم"))));
        _services.AddRange(serviceIds.Distinct().Select(id =>
            new DiscountService(this, ValidateId(id, "الخدمة"))));
        _packages.AddRange(packageIds.Distinct().Select(id =>
            new DiscountPackage(this, ValidateId(id, "الباقة"))));
    }

    private static void ValidateDefinition(DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt,
        IReadOnlyCollection<long> departmentIds, IReadOnlyCollection<long> serviceIds,
        IReadOnlyCollection<long> packageIds)
    {
        if (!Enum.IsDefined(type) || !Enum.IsDefined(appliesTo) ||
            !Enum.IsDefined(scopeMode) || endAt <= startAt ||
            decimal.Round(value, 2) != value || value < 0 || value > MaximumMoney ||
            type == DiscountType.Percentage && value > 100 ||
            type == DiscountType.FixedAmount && value <= 0)
        {
            throw new DomainException("بيانات الخصم أو فترة سريانه غير صحيحة.");
        }

        bool all = scopeMode == DiscountScopeMode.All;
        if (all && (departmentIds.Count != 0 || serviceIds.Count != 0 || packageIds.Count != 0) ||
            !all && departmentIds.Count == 0 && serviceIds.Count == 0 && packageIds.Count == 0 ||
            appliesTo == DiscountAppliesTo.Bookings && packageIds.Count != 0 ||
            appliesTo == DiscountAppliesTo.Packages && serviceIds.Count != 0 ||
            appliesTo == DiscountAppliesTo.Both && !all && departmentIds.Count == 0 &&
                (serviceIds.Count == 0 || packageIds.Count == 0))
        {
            throw new DomainException("نطاق الخصم لا يطابق مجال تطبيقه.");
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new DomainException("اسم الخصم مطلوب ولا يتجاوز 200 حرف.");
        }
        return name.Trim();
    }

    private static long ValidateId(long id, string name) => id > 0
        ? id
        : throw new DomainException($"معرف {name} غير صحيح.");

    private void Touch(long actorUserId, DateTimeOffset updatedAt)
    {
        UpdatedByAdminUserId = ValidateId(actorUserId, "الأدمن");
        UpdatedAt = NormalizeTimestamp(updatedAt);
    }

    private void EnsureEditable()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل خصم مؤرشف.");
        }
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) =>
        DateTimeOffset.FromUnixTimeSeconds(timestamp.ToUnixTimeSeconds());
}

public sealed class DiscountDepartment
{
    private DiscountDepartment() { }
    internal DiscountDepartment(Discount discount, long departmentId) =>
        (Discount, DiscountId, DepartmentId) = (discount, discount.Id, departmentId);

    public long DiscountId { get; private set; }
    public Discount Discount { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
}

public sealed class DiscountService
{
    private DiscountService() { }
    internal DiscountService(Discount discount, long serviceId) =>
        (Discount, DiscountId, ServiceId) = (discount, discount.Id, serviceId);

    public long DiscountId { get; private set; }
    public Discount Discount { get; private set; } = null!;
    public long ServiceId { get; private set; }
    public Service Service { get; private set; } = null!;
}

public sealed class DiscountPackage
{
    private DiscountPackage() { }
    internal DiscountPackage(Discount discount, long packageId) =>
        (Discount, DiscountId, PackageId) = (discount, discount.Id, packageId);

    public long DiscountId { get; private set; }
    public Discount Discount { get; private set; } = null!;
    public long PackageId { get; private set; }
    public Package Package { get; private set; } = null!;
}
