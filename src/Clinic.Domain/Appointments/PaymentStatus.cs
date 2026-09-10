namespace Clinic.Domain.Appointments;

public enum PaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
    CoveredByPackage = 5
}
