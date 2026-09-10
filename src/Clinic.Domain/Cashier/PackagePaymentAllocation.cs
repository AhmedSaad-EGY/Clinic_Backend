using Clinic.Domain.Common;
using Clinic.Domain.Packages;

namespace Clinic.Domain.Cashier;

public sealed class PackagePaymentAllocation : Entity
{
    private PackagePaymentAllocation()
    {
    }

    internal PackagePaymentAllocation(Payment payment, PatientPackage patientPackage,
        decimal amount)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(patientPackage);
        CashierGuard.PositiveMoney(amount, "مبلغ الباقة");
        if (patientPackage.PatientId != payment.PatientId)
        {
            throw new DomainException("الباقة وعملية التحصيل لا تخصان المريض نفسه.");
        }
        if (patientPackage.PaymentStatus != PatientPackagePaymentStatus.Paid ||
            amount != patientPackage.NetPriceSnapshot)
        {
            throw new DomainException("يجب ربط القيمة الكاملة لباقة مدفوعة.");
        }

        Payment = payment;
        PatientPackage = patientPackage;
        PatientPackageId = patientPackage.Id;
        PatientId = patientPackage.PatientId;
        Amount = amount;
    }

    public long PaymentId { get; private set; }
    public Payment Payment { get; private set; } = null!;
    public long PatientPackageId { get; private set; }
    public PatientPackage PatientPackage { get; private set; } = null!;
    public long PatientId { get; private set; }
    public decimal Amount { get; private set; }
}
