namespace Clinic.Domain.Appointments;

public sealed class AppointmentDevice : Entity
{
    private AppointmentDevice() { }

    internal AppointmentDevice(AppointmentService appointmentService,
        long serviceDeviceId, long serviceId, long departmentId, long deviceId,
        DateTimeOffset reservedFrom, DateTimeOffset reservedTo)
    {
        ArgumentNullException.ThrowIfNull(appointmentService);
        AppointmentGuard.PositiveId(serviceDeviceId, "ربط الجهاز بالخدمة");
        AppointmentGuard.PositiveId(deviceId, "الجهاز");
        AppointmentGuard.ValidRange(reservedFrom, reservedTo);
        if (appointmentService.ServiceId != serviceId ||
            appointmentService.DepartmentId != departmentId)
        {
            throw new DomainException("يجب أن يكون الجهاز مهيأ للخدمة والقسم نفسيهما.");
        }

        AppointmentService = appointmentService;
        ServiceDeviceId = serviceDeviceId;
        ServiceId = serviceId;
        DepartmentId = departmentId;
        DeviceId = deviceId;
        ReservedFrom = reservedFrom;
        ReservedTo = reservedTo;
    }

    public long AppointmentServiceId { get; private set; }
    public AppointmentService AppointmentService { get; private set; } = null!;
    public long ServiceDeviceId { get; private set; }
    public ServiceDevice ServiceDevice { get; private set; } = null!;
    public long ServiceId { get; private set; }
    public long DepartmentId { get; private set; }
    public long DeviceId { get; private set; }
    public Device Device { get; private set; } = null!;
    public DateTimeOffset ReservedFrom { get; private set; }
    public DateTimeOffset ReservedTo { get; private set; }
}
