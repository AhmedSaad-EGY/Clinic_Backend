namespace Clinic.Domain.Cashier;

public sealed class Refund : AggregateRoot
{
    private readonly List<RefundMethodAllocation> _methodAllocations = [];
    private readonly List<RefundAppointmentAllocation> _appointmentAllocations = [];

    private Refund() { }

    private Refund(string transactionNumber, Guid idempotencyKey,
        string requestFingerprint, long approvalRequestId, long originalPaymentId,
        long executionShiftId, long executedByUserId, DateTimeOffset executedAt,
        string? note)
    {
        TransactionNumber = CashierGuard.RequiredText(transactionNumber,
            "رقم العملية", 40);
        if (idempotencyKey == Guid.Empty || requestFingerprint.Length != 64 ||
            approvalRequestId <= 0 || originalPaymentId <= 0 ||
            executionShiftId <= 0 || executedByUserId <= 0)
        {
            throw new DomainException("بيانات عملية الاسترداد غير صحيحة.");
        }

        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        ApprovalRequestId = approvalRequestId;
        OriginalPaymentId = originalPaymentId;
        ExecutionShiftId = executionShiftId;
        ExecutedByUserId = executedByUserId;
        ExecutedAt = executedAt;
        Note = CashierGuard.OptionalText(note, 500);
        Status = RefundStatus.Posted;
    }

    public string TransactionNumber { get; private set; } = string.Empty;
    public Guid IdempotencyKey { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public long ApprovalRequestId { get; private set; }
    public long OriginalPaymentId { get; private set; }
    public long ExecutionShiftId { get; private set; }
    public decimal Amount { get; private set; }
    public long ExecutedByUserId { get; private set; }
    public DateTimeOffset ExecutedAt { get; private set; }
    public RefundStatus Status { get; private set; }
    public string? Note { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RefundMethodAllocation> MethodAllocations =>
        _methodAllocations;
    public IReadOnlyCollection<RefundAppointmentAllocation> AppointmentAllocations =>
        _appointmentAllocations;

    public static Refund Create(string transactionNumber, Guid idempotencyKey,
        string requestFingerprint, long approvalRequestId, long originalPaymentId,
        long executionShiftId, long executedByUserId, DateTimeOffset executedAt,
        string? note,
        IReadOnlyCollection<(long OriginalAllocationId, bool IsCash,
            decimal Amount, string? ReferenceNumber)> methods,
        long originalAppointmentAllocationId, decimal appointmentAmount)
    {
        if (methods.Count is < 1 or > 4 ||
            methods.Select(item => item.OriginalAllocationId).Distinct().Count() !=
                methods.Count)
        {
            throw new DomainException("وسائل الاسترداد غير صحيحة أو مكررة.");
        }

        Refund refund = new(transactionNumber, idempotencyKey, requestFingerprint,
            approvalRequestId, originalPaymentId, executionShiftId,
            executedByUserId, executedAt, note);
        refund._methodAllocations.AddRange(methods.Select(item =>
            new RefundMethodAllocation(refund, item.OriginalAllocationId,
                originalPaymentId, item.IsCash, item.Amount,
                item.ReferenceNumber)));
        refund._appointmentAllocations.Add(new RefundAppointmentAllocation(refund,
            originalAppointmentAllocationId, originalPaymentId,
            appointmentAmount));

        decimal methodTotal = refund._methodAllocations.Sum(item => item.Amount);
        CashierGuard.PositiveMoney(appointmentAmount, "مبلغ الاسترداد");
        if (methodTotal != appointmentAmount)
        {
            throw new DomainException("مجموع وسائل الاسترداد يجب أن يساوي المبلغ المسترد.");
        }

        refund.Amount = appointmentAmount;
        return refund;
    }
}
