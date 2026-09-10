using Clinic.Domain.Common;
using Clinic.Domain.Catalog;
using Clinic.Domain.Packages;
using Clinic.Domain.Scheduling;

namespace Clinic.Domain.Appointments;

public sealed class AppointmentService : Entity
{
    private readonly List<AppointmentDevice> _devices = [];

    private AppointmentService() { }

    internal AppointmentService(Appointment appointment, long serviceId,
        long doctorServiceId, long departmentId, int sequenceNumber,
        DateTimeOffset segmentStartAt, DateTimeOffset segmentEndAt,
        int quantity, decimal unitPrice)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        AppointmentGuard.PositiveId(serviceId, "الخدمة");
        AppointmentGuard.PositiveId(doctorServiceId, "إسناد الطبيب");
        AppointmentGuard.ValidRange(segmentStartAt, segmentEndAt);
        if (appointment.DepartmentId != departmentId)
        {
            throw new DomainException("يجب أن تنتمي جميع خدمات الحجز إلى القسم نفسه.");
        }

        if (sequenceNumber <= 0 || quantity <= 0 || unitPrice <= 0)
        {
            throw new DomainException("بيانات خدمة الحجز غير صحيحة.");
        }

        Appointment = appointment;
        ServiceId = serviceId;
        DoctorServiceId = doctorServiceId;
        DepartmentId = departmentId;
        SequenceNumber = sequenceNumber;
        SegmentStartAt = segmentStartAt;
        SegmentEndAt = segmentEndAt;
        Quantity = quantity;
        UnitPrice = unitPrice;
        GrossAmount = unitPrice * quantity;
        NetAmount = GrossAmount;
        Status = AppointmentServiceStatus.Scheduled;
    }

    public long AppointmentId { get; private set; }
    public Appointment Appointment { get; private set; } = null!;
    public long ServiceId { get; private set; }
    public Service Service { get; private set; } = null!;
    public long DoctorServiceId { get; private set; }
    public DoctorService DoctorService { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public int SequenceNumber { get; private set; }
    public DateTimeOffset SegmentStartAt { get; private set; }
    public DateTimeOffset SegmentEndAt { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal PackageCoveredAmount { get; private set; }
    public AppointmentServiceStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public PackageSessionBooking? PackageSessionBooking { get; private set; }
    public IReadOnlyCollection<AppointmentDevice> Devices => _devices;

    internal void AddDevice(long serviceDeviceId, long deviceId)
    {
        if (_devices.Any(item => item.ServiceDeviceId == serviceDeviceId))
        {
            throw new DomainException("لا يمكن تكرار الجهاز داخل خدمة الحجز.");
        }

        _devices.Add(new AppointmentDevice(this, serviceDeviceId, ServiceId,
            DepartmentId, deviceId, SegmentStartAt, SegmentEndAt));
    }

    internal void Complete() => Status = AppointmentServiceStatus.Completed;
    internal void Cancel() => Status = AppointmentServiceStatus.Cancelled;
    internal void Supersede() => Status = AppointmentServiceStatus.Superseded;

    internal void CoverByPackage(decimal coveredAmount)
    {
        if (coveredAmount <= 0 || coveredAmount != GrossAmount)
        {
            throw new DomainException("قيمة تغطية الباقة لا تطابق قيمة الخدمة.");
        }

        PackageCoveredAmount = coveredAmount;
        NetAmount = 0;
    }

    internal void ChangeDoctorService(long doctorServiceId)
    {
        AppointmentGuard.PositiveId(doctorServiceId, "إسناد الطبيب");
        if (Status != AppointmentServiceStatus.Scheduled)
        {
            throw new DomainException("لا يمكن تغيير طبيب خدمة منتهية.");
        }
        DoctorServiceId = doctorServiceId;
    }

    internal void AttachPackageBooking(PackageSessionBooking booking) =>
        PackageSessionBooking = booking;
}
