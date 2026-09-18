namespace Clinic.Domain.Packages;

public sealed class PackageSession : Entity
{
    private PackageSession()
    {
    }

    internal PackageSession(PatientPackageService service, int sequenceNumber)
    {
        PatientPackageService = service;
        PatientPackageServiceId = service.Id;
        PatientPackageId = service.PatientPackageId;
        ServiceId = service.ServiceId;
        SequenceNumber = sequenceNumber;
        UnitPriceSnapshot = service.UnitPriceSnapshot;
        Status = PackageSessionStatus.Available;
    }

    public long PatientPackageServiceId { get; private set; }
    public PatientPackageService PatientPackageService { get; private set; } = null!;
    public long PatientPackageId { get; private set; }
    public long ServiceId { get; private set; }
    public int SequenceNumber { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public PackageSessionStatus Status { get; private set; }
    public DateTimeOffset? ReservedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public PackageSessionBooking Reserve(Appointments.Appointment appointment,
        Appointments.AppointmentService appointmentService, DateTimeOffset reservedAt)
    {
        if (Status != PackageSessionStatus.Available ||
            appointment.PatientPackageId != PatientPackageId ||
            appointmentService.ServiceId != ServiceId ||
            appointmentService.AppointmentId != appointment.Id)
        {
            throw new DomainException("لا يمكن حجز جلسة الباقة لهذه الخدمة.");
        }

        Status = PackageSessionStatus.Reserved;
        ReservedAt = reservedAt;
        ConsumedAt = null;
        return PackageSessionBooking.Create(this, appointment, appointmentService, reservedAt);
    }

    internal void Release()
    {
        if (Status != PackageSessionStatus.Reserved)
        {
            throw new DomainException("لا يمكن تحرير جلسة غير محجوزة.");
        }

        Status = PackageSessionStatus.Available;
        ReservedAt = null;
        ConsumedAt = null;
    }

    internal void Consume(DateTimeOffset consumedAt)
    {
        if (Status != PackageSessionStatus.Reserved)
        {
            throw new DomainException("لا يمكن استهلاك جلسة غير محجوزة.");
        }

        Status = PackageSessionStatus.Consumed;
        ConsumedAt = consumedAt;
    }
}
