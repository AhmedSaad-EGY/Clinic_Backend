using Clinic.Domain.Common;

namespace Clinic.Domain.Catalog;

public sealed class Service : AggregateRoot
{
    private readonly List<ServiceDevice> _deviceAssignments = [];
    private readonly List<ServicePriceHistory> _priceHistory = [];

    private Service()
    {
    }

    private Service(
        long departmentId,
        long specializationId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        decimal unitPrice,
        long changedByUserId,
        DateTimeOffset createdAt)
    {
        CatalogGuard.PositiveIdentifier(departmentId, "القسم");
        CatalogGuard.PositiveIdentifier(specializationId, "التخصص");
        ValidateDetails(name, serviceType, durationMinutes, pricingMode);
        CatalogGuard.PositiveMoney(unitPrice);

        DepartmentId = departmentId;
        SpecializationId = specializationId;
        Name = name.Trim();
        ServiceType = serviceType;
        DurationMinutes = durationMinutes;
        PricingMode = pricingMode;
        CurrentUnitPrice = unitPrice;
        IsActive = true;
        _priceHistory.Add(new ServicePriceHistory(this, unitPrice, createdAt, changedByUserId));
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public long SpecializationId { get; private set; }

    public Specialization Specialization { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public ServiceType ServiceType { get; private set; }

    public int DurationMinutes { get; private set; }

    public PricingMode PricingMode { get; private set; }

    public decimal CurrentUnitPrice { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ServicePriceHistory> PriceHistory => _priceHistory;

    public IReadOnlyCollection<ServiceDevice> DeviceAssignments => _deviceAssignments;

    public static Service Create(
        long departmentId,
        long specializationId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        decimal unitPrice,
        long changedByUserId,
        DateTimeOffset createdAt) =>
        new(
            departmentId,
            specializationId,
            name,
            serviceType,
            durationMinutes,
            pricingMode,
            unitPrice,
            changedByUserId,
            createdAt);

    public void UpdateDetails(
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode)
    {
        EnsureNotArchived();
        ValidateDetails(name, serviceType, durationMinutes, pricingMode);
        Name = name.Trim();
        ServiceType = serviceType;
        DurationMinutes = durationMinutes;
        PricingMode = pricingMode;
    }

    public void ChangePrice(decimal unitPrice, long changedByUserId, DateTimeOffset changedAt)
    {
        EnsureNotArchived();
        CatalogGuard.PositiveMoney(unitPrice);
        CatalogGuard.PositiveIdentifier(changedByUserId, "المستخدم");

        if (unitPrice == CurrentUnitPrice)
        {
            throw new DomainException("السعر الجديد يطابق السعر الحالي.");
        }

        ServicePriceHistory current = _priceHistory.SingleOrDefault(item => item.EffectiveTo is null)
            ?? throw new DomainException("لا يوجد سعر حالي صالح للخدمة.");

        current.Close(changedAt);
        CurrentUnitPrice = unitPrice;
        _priceHistory.Add(new ServicePriceHistory(this, unitPrice, changedAt, changedByUserId));
    }

    public void ReplaceDeviceAssignments(
        IReadOnlyCollection<(Device Device, bool IsRequired)> assignments)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(assignments);

        if (assignments.Select(item => item.Device.Id).Distinct().Count() != assignments.Count)
        {
            throw new DomainException("لا يمكن تكرار الجهاز داخل الخدمة.");
        }

        foreach (ServiceDevice existing in _deviceAssignments)
        {
            (Device Device, bool IsRequired)? requested = assignments
                .Select(item => ((Device Device, bool IsRequired)?)item)
                .SingleOrDefault(item => item!.Value.Device.Id == existing.DeviceId);

            if (requested is null)
            {
                existing.Deactivate();
            }
            else
            {
                existing.Update(requested.Value.IsRequired);
            }
        }

        HashSet<long> existingDeviceIds = _deviceAssignments
            .Select(item => item.DeviceId)
            .ToHashSet();

        foreach ((Device device, bool isRequired) in assignments)
        {
            if (device.IsArchived || !device.IsActive)
            {
                throw new DomainException("لا يمكن ربط جهاز غير متاح بالخدمة.");
            }

            if (device.DepartmentId != DepartmentId)
            {
                throw new DomainException("يجب أن تنتمي الخدمة والجهاز إلى نفس القسم.");
            }

            if (!existingDeviceIds.Contains(device.Id))
            {
                _deviceAssignments.Add(ServiceDevice.Create(this, device, isRequired));
            }
        }
    }

    public void Archive()
    {
        EnsureNotArchived();
        IsActive = false;
        IsArchived = true;

        foreach (ServiceDevice assignment in _deviceAssignments)
        {
            assignment.Deactivate();
        }
    }

    private static void ValidateDetails(
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode)
    {
        _ = CatalogGuard.RequiredText(name, "اسم الخدمة", 200);

        if (!Enum.IsDefined(serviceType))
        {
            throw new DomainException("نوع الخدمة غير صحيح.");
        }

        if (!Enum.IsDefined(pricingMode))
        {
            throw new DomainException("طريقة التسعير غير صحيحة.");
        }

        if (durationMinutes <= 0 || durationMinutes > 1440)
        {
            throw new DomainException("مدة الخدمة يجب أن تكون بين دقيقة و1440 دقيقة.");
        }
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل خدمة مؤرشفة.");
        }
    }
}
