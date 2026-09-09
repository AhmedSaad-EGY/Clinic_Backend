using Clinic.Domain.Catalog;
using Clinic.Domain.Common;

namespace Clinic.Domain.Packages;

public sealed class PackageService : Entity
{
    private PackageService()
    {
    }

    internal PackageService(Package package, Service service, int sessionsIncluded,
        decimal unitPriceAtDefinition)
    {
        Package = package;
        PackageId = package.Id;
        DepartmentId = package.DepartmentId;
        Service = service;
        ServiceId = service.Id;
        Update(service, sessionsIncluded, unitPriceAtDefinition);
    }

    public long PackageId { get; private set; }
    public Package Package { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public long ServiceId { get; private set; }
    public Service Service { get; private set; } = null!;
    public int SessionsIncluded { get; private set; }
    public decimal UnitPriceAtDefinition { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void Update(Service service, int sessionsIncluded, decimal unitPriceAtDefinition)
    {
        EnsureEligibleService(service);
        if (sessionsIncluded <= 0)
        {
            throw new DomainException("عدد الجلسات داخل الباقة يجب أن يكون أكبر من صفر.");
        }

        if (unitPriceAtDefinition <= 0 || unitPriceAtDefinition > Package.MaximumMoney ||
            decimal.Round(unitPriceAtDefinition, 2) != unitPriceAtDefinition)
        {
            throw new DomainException(
                "سعر الجلسة داخل تعريف الباقة خارج النطاق المسموح.");
        }

        SessionsIncluded = sessionsIncluded;
        UnitPriceAtDefinition = unitPriceAtDefinition;
        IsActive = true;
    }

    internal void Deactivate() => IsActive = false;

    private void EnsureEligibleService(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (service.DepartmentId != DepartmentId)
        {
            throw new DomainException("يجب أن تنتمي كل خدمات الباقة إلى قسم الباقة.");
        }

        if (service.IsArchived || !service.IsActive)
        {
            throw new DomainException("لا يمكن إضافة خدمة غير متاحة إلى الباقة.");
        }

        if (service.PricingMode != PricingMode.Fixed)
        {
            throw new DomainException("الباقات الحالية تدعم الخدمات ذات السعر الثابت فقط.");
        }
    }
}
