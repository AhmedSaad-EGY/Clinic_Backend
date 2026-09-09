using Clinic.Domain.Common;

namespace Clinic.Domain.Cashier;

public sealed class RefundAppointmentAllocation : Entity
{
    private RefundAppointmentAllocation() { }

    internal RefundAppointmentAllocation(Refund refund, long originalAllocationId,
        long originalPaymentId, decimal amount)
    {
        CashierGuard.PositiveId(originalAllocationId, "تخصيص الحجز الأصلي");
        CashierGuard.PositiveId(originalPaymentId, "التحصيل الأصلي");
        CashierGuard.PositiveMoney(amount, "مبلغ الحجز المسترد");
        Refund = refund;
        OriginalAllocationId = originalAllocationId;
        OriginalPaymentId = originalPaymentId;
        Amount = amount;
    }

    public long RefundId { get; private set; }
    public Refund Refund { get; private set; } = null!;
    public long OriginalAllocationId { get; private set; }
    public AppointmentPaymentAllocation OriginalAllocation { get; private set; } = null!;
    public long OriginalPaymentId { get; private set; }
    public decimal Amount { get; private set; }
}
