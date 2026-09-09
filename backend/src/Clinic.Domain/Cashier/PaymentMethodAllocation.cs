using Clinic.Domain.Common;

namespace Clinic.Domain.Cashier;

public sealed class PaymentMethodAllocation : Entity
{
    private PaymentMethodAllocation()
    {
    }

    internal PaymentMethodAllocation(Payment payment, long paymentMethodId,
        bool isCash, decimal amount, string? referenceNumber)
    {
        CashierGuard.PositiveId(paymentMethodId, "وسيلة الدفع");
        CashierGuard.PositiveMoney(amount, "مبلغ وسيلة الدفع");
        string? reference = CashierGuard.OptionalText(referenceNumber, 100);
        if (isCash && reference is not null)
        {
            throw new DomainException("لا يُسجل رقم مرجع مع الدفع النقدي.");
        }

        if (!isCash && reference is null)
        {
            throw new DomainException("رقم المرجع مطلوب لوسيلة الدفع الإلكترونية.");
        }

        Payment = payment;
        PaymentMethodId = paymentMethodId;
        Amount = amount;
        ReferenceNumber = reference;
    }

    public long PaymentId { get; private set; }

    public Payment Payment { get; private set; } = null!;

    public long PaymentMethodId { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string? ReferenceNumber { get; private set; }
}
