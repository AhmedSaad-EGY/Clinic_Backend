namespace Clinic.Domain.Packages;

public sealed class PackageSessionBooking : Entity
{
    private PackageSessionBooking() { }

    private PackageSessionBooking(PackageSession session, Appointment appointment,
        AppointmentService appointmentService, DateTimeOffset reservedAt)
    {
        Session = session;
        PackageSessionId = session.Id;
        PatientPackageId = session.PatientPackageId;
        Appointment = appointment;
        AppointmentId = appointment.Id;
        AppointmentService = appointmentService;
        AppointmentServiceId = appointmentService.Id;
        PatientId = appointment.PatientId;
        ServiceId = appointmentService.ServiceId;
        Status = PackageSessionBookingStatus.Reserved;
        ReservedAt = reservedAt;
        appointmentService.AttachPackageBooking(this);
    }

    public long PackageSessionId { get; private set; }
    public PackageSession Session { get; private set; } = null!;
    public long PatientPackageId { get; private set; }
    public long AppointmentId { get; private set; }
    public Appointment Appointment { get; private set; } = null!;
    public long AppointmentServiceId { get; private set; }
    public AppointmentService AppointmentService { get; private set; } = null!;
    public long PatientId { get; private set; }
    public long ServiceId { get; private set; }
    public PackageSessionBookingStatus Status { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal static PackageSessionBooking Create(PackageSession session,
        Appointment appointment, AppointmentService appointmentService,
        DateTimeOffset reservedAt) => new(session, appointment, appointmentService, reservedAt);

    public void Release(DateTimeOffset releasedAt)
    {
        if (Status != PackageSessionBookingStatus.Reserved)
        {
            throw new DomainException("رابط جلسة الباقة ليس محجوزًا.");
        }

        Session.Release();
        Status = PackageSessionBookingStatus.Released;
        ReleasedAt = releasedAt;
    }

    public void Consume(DateTimeOffset consumedAt)
    {
        if (Status != PackageSessionBookingStatus.Reserved)
        {
            throw new DomainException("رابط جلسة الباقة ليس محجوزًا.");
        }

        Session.Consume(consumedAt);
        Status = PackageSessionBookingStatus.Consumed;
        ConsumedAt = consumedAt;
    }
}
