namespace Clinic.Domain.Cashier;

public sealed class AppointmentPaymentAllocation : Entity
{
    private AppointmentPaymentAllocation()
    {
    }

    internal AppointmentPaymentAllocation(Payment payment, long appointmentId,
        long patientId, decimal amount)
    {
        CashierGuard.PositiveId(appointmentId, "الحجز");
        CashierGuard.PositiveId(patientId, "المريض");
        CashierGuard.PositiveMoney(amount, "مبلغ الحجز");
        Payment = payment;
        AppointmentId = appointmentId;
        PatientId = patientId;
        Amount = amount;
    }

    public long PaymentId { get; private set; }

    public Payment Payment { get; private set; } = null!;

    public long AppointmentId { get; private set; }

    public Appointment Appointment { get; private set; } = null!;

    public long PatientId { get; private set; }

    public decimal Amount { get; private set; }
}
