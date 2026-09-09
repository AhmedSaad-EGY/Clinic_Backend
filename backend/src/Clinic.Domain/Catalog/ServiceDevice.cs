using Clinic.Domain.Common;

namespace Clinic.Domain.Catalog;

public sealed class ServiceDevice : Entity
{
    private ServiceDevice()
    {
    }

    private ServiceDevice(Service service, Device device, bool isRequired)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(device);

        if (service.DepartmentId != device.DepartmentId)
        {
            throw new DomainException("يجب أن تنتمي الخدمة والجهاز إلى نفس القسم.");
        }

        Service = service;
        ServiceId = service.Id;
        Device = device;
        DeviceId = device.Id;
        DepartmentId = service.DepartmentId;
        IsRequired = isRequired;
        IsActive = true;
    }

    public long ServiceId { get; private set; }

    public Service Service { get; private set; } = null!;

    public long DeviceId { get; private set; }

    public Device Device { get; private set; } = null!;

    public long DepartmentId { get; private set; }

    public bool IsRequired { get; private set; }

    public bool IsActive { get; private set; }

    internal static ServiceDevice Create(Service service, Device device, bool isRequired) =>
        new(service, device, isRequired);

    internal void Update(bool isRequired)
    {
        IsRequired = isRequired;
        IsActive = true;
    }

    internal void Deactivate() => IsActive = false;
}
