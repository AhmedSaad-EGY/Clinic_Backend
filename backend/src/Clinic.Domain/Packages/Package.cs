using Clinic.Domain.Catalog;
using Clinic.Domain.Common;

namespace Clinic.Domain.Packages;

public sealed class Package : AggregateRoot
{
    public const int MaximumServiceCount = 20;
    public const int MaximumSessionCount = 500;
    public const int MaximumDurationDays = 36500;
    public const decimal MaximumMoney = 9999999999999999.99m;
    private readonly List<PackageService> _services = [];

    private Package()
    {
    }

    private Package(long departmentId, string name, decimal basePrice, int activationGraceDays,
        int usageDurationDays, long actorUserId, DateTimeOffset createdAt)
    {
        ValidateIdentifier(departmentId, "القسم");
        ValidateIdentifier(actorUserId, "المستخدم");
        DepartmentId = departmentId;
        Name = ValidateName(name);
        BasePrice = ValidateBasePrice(basePrice);
        ActivationGraceDays = ValidateDuration(activationGraceDays, "مهلة بدء الاستخدام");
        UsageDurationDays = ValidateDuration(usageDurationDays, "مدة الاستخدام");
        CreatedByAdminUserId = actorUserId;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public long DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public int SessionCount { get; private set; }
    public decimal BasePrice { get; private set; }
    public int? ActivationGraceDays { get; private set; }
    public int? UsageDurationDays { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public long CreatedByAdminUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? UpdatedByAdminUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PackageService> Services => _services;

    public static Package Create(long departmentId, string name, decimal basePrice,
        int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<(Service Service, int SessionsIncluded, decimal UnitPrice)> services,
        long actorUserId, DateTimeOffset createdAt)
    {
        Package package = new(departmentId, name, basePrice, activationGraceDays,
            usageDurationDays, actorUserId, createdAt);
        package.ReplaceServices(services, actorUserId, createdAt);
        package.UpdatedByAdminUserId = null;
        package.UpdatedAt = null;
        return package;
    }

    public void Update(string name, decimal basePrice, int activationGraceDays,
        int usageDurationDays,
        IReadOnlyCollection<(Service Service, int SessionsIncluded, decimal UnitPrice)> services,
        long actorUserId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        Name = ValidateName(name);
        BasePrice = ValidateBasePrice(basePrice);
        ActivationGraceDays = ValidateDuration(activationGraceDays, "مهلة بدء الاستخدام");
        UsageDurationDays = ValidateDuration(usageDurationDays, "مدة الاستخدام");
        ReplaceServices(services, actorUserId, updatedAt);
    }

    public void SetActive(bool isActive, long actorUserId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        if (isActive && _services.All(item => !item.IsActive))
        {
            throw new DomainException("لا يمكن تفعيل باقة لا تحتوي على خدمات فعالة.");
        }

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

    private void ReplaceServices(
        IReadOnlyCollection<(Service Service, int SessionsIncluded, decimal UnitPrice)> services,
        long actorUserId, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Count is < 1 or > MaximumServiceCount)
        {
            throw new DomainException($"يجب أن تحتوي الباقة على خدمة واحدة إلى {MaximumServiceCount} خدمة.");
        }

        if (services.Select(item => item.Service.Id).Distinct().Count() != services.Count)
        {
            throw new DomainException("لا يمكن تكرار الخدمة داخل الباقة.");
        }

        long totalSessions = services.Sum(item => (long)item.SessionsIncluded);
        if (totalSessions is < 1 or > MaximumSessionCount)
        {
            throw new DomainException($"إجمالي جلسات الباقة يجب أن يكون بين 1 و{MaximumSessionCount}.");
        }

        foreach (PackageService existing in _services)
        {
            var requested = services.SingleOrDefault(item => item.Service.Id == existing.ServiceId);
            if (requested.Service is null)
            {
                existing.Deactivate();
            }
            else
            {
                existing.Update(requested.Service, requested.SessionsIncluded, requested.UnitPrice);
            }
        }

        HashSet<long> existingServiceIds = _services.Select(item => item.ServiceId).ToHashSet();
        foreach ((Service service, int sessionsIncluded, decimal unitPrice) in services)
        {
            if (!existingServiceIds.Contains(service.Id))
            {
                _services.Add(new PackageService(this, service, sessionsIncluded, unitPrice));
            }
        }

        SessionCount = checked((int)totalSessions);
        Touch(actorUserId, updatedAt);
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new DomainException("اسم الباقة مطلوب ولا يجب أن يتجاوز 200 حرف.");
        }

        return name.Trim();
    }

    private static decimal ValidateBasePrice(decimal value)
    {
        if (value < 0 || value > MaximumMoney || decimal.Round(value, 2) != value)
        {
            throw new DomainException(
                "سعر الباقة خارج النطاق المسموح أو يحتوي على أكثر من منزلتين عشريتين.");
        }

        return value;
    }

    private static int ValidateDuration(int value, string fieldName)
    {
        if (value is <= 0 or > MaximumDurationDays)
        {
            throw new DomainException(
                $"{fieldName} يجب أن تكون بين يوم واحد و{MaximumDurationDays} يوم.");
        }

        return value;
    }

    private static void ValidateIdentifier(long value, string fieldName)
    {
        if (value <= 0)
        {
            throw new DomainException($"{fieldName} غير صحيح.");
        }
    }

    private void Touch(long actorUserId, DateTimeOffset updatedAt)
    {
        ValidateIdentifier(actorUserId, "المستخدم");
        UpdatedByAdminUserId = actorUserId;
        UpdatedAt = updatedAt;
    }

    private void EnsureEditable()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل باقة مؤرشفة.");
        }
    }
}
