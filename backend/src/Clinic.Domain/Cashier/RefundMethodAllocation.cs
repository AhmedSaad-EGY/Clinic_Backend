using Clinic.Domain.Common;

namespace Clinic.Domain.Cashier;

public sealed class RefundMethodAllocation : Entity
{
    private RefundMethodAllocation() { }

    internal RefundMethodAllocation(Refund refund, long originalAllocationId,
        long originalPaymentId, bool isCash, decimal amount,
        string? referenceNumber)
    {
        CashierGuard.PositiveId(originalAllocationId, "تخصيص وسيلة الدفع الأصلية");
        CashierGuard.PositiveId(originalPaymentId, "التحصيل الأصلي");
        CashierGuard.PositiveMoney(amount, "مبلغ وسيلة الاسترداد");
        string? reference = CashierGuard.OptionalText(referenceNumber, 100);
        if (isCash && reference is not null)
        {
            throw new DomainException("لا يُسجل رقم مرجع مع الاسترداد النقدي.");
        }

        if (!isCash && reference is null)
        {
            throw new DomainException("رقم المرجع مطلوب للاسترداد الإلكتروني.");
        }

        Refund = refund;
        OriginalAllocationId = originalAllocationId;
        OriginalPaymentId = originalPaymentId;
        Amount = amount;
        ReferenceNumber = reference;
    }

    public long RefundId { get; private set; }
    public Refund Refund { get; private set; } = null!;
    public long OriginalAllocationId { get; private set; }
    public PaymentMethodAllocation OriginalAllocation { get; private set; } = null!;
    public long OriginalPaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string? ReferenceNumber { get; private set; }
}
