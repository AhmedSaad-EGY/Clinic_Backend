namespace Clinic.Domain.Cashier;

public sealed class CashWithdrawal : AggregateRoot
{
    public const decimal MaximumAmount = 9999999999999999.99m;

    private CashWithdrawal()
    {
    }

    private CashWithdrawal(string withdrawalNumber, Guid idempotencyKey,
        string requestFingerprint, long shiftId, decimal amount, string reason,
        long requestedByUserId, DateTimeOffset requestedAt)
    {
        WithdrawalNumber = CashierGuard.RequiredText(withdrawalNumber,
            "رقم طلب السحب", 40);
        if (idempotencyKey == Guid.Empty)
        {
            throw new DomainException("مفتاح منع التكرار غير صحيح.");
        }

        RequestFingerprint = CashierGuard.RequiredText(requestFingerprint,
            "بصمة الطلب", 64);
        if (RequestFingerprint.Length != 64)
        {
            throw new DomainException("بصمة طلب السحب غير صحيحة.");
        }

        CashierGuard.PositiveId(shiftId, "الشيفت");
        CashierGuard.PositiveMoney(amount, "مبلغ السحب");
        if (amount > MaximumAmount)
        {
            throw new DomainException("مبلغ السحب أكبر من الحد المسموح.");
        }

        CashierGuard.PositiveId(requestedByUserId, "السكرتيرة");
        IdempotencyKey = idempotencyKey;
        ShiftId = shiftId;
        Amount = amount;
        Reason = CashierGuard.RequiredText(reason, "سبب السحب", 500);
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        Status = CashWithdrawalStatus.Pending;
    }

    public string WithdrawalNumber { get; private set; } = string.Empty;

    public Guid IdempotencyKey { get; private set; }

    public string RequestFingerprint { get; private set; } = string.Empty;

    public long ShiftId { get; private set; }

    public Shift Shift { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public CashWithdrawalStatus Status { get; private set; }

    public long RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public long? ReviewedByAdminUserId { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? DecisionReason { get; private set; }

    public Guid? ExecutionIdempotencyKey { get; private set; }

    public long? ExecutedByUserId { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public long? CancelledByUserId { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static CashWithdrawal Create(string withdrawalNumber,
        Guid idempotencyKey, string requestFingerprint, long shiftId,
        decimal amount, string reason, long requestedByUserId,
        DateTimeOffset requestedAt) => new(withdrawalNumber, idempotencyKey,
            requestFingerprint, shiftId, amount, reason, requestedByUserId,
            requestedAt);

    public void Approve(long adminUserId, DateTimeOffset reviewedAt,
        string decisionReason) => Review(adminUserId, reviewedAt,
            decisionReason, CashWithdrawalStatus.Approved);

    public void Reject(long adminUserId, DateTimeOffset reviewedAt,
        string decisionReason) => Review(adminUserId, reviewedAt,
            decisionReason, CashWithdrawalStatus.Rejected);

    public void Cancel(long actorUserId, DateTimeOffset cancelledAt)
    {
        EnsureStatus(CashWithdrawalStatus.Pending,
            "لا يمكن إلغاء طلب السحب بعد مراجعته.");
        if (actorUserId != RequestedByUserId)
        {
            throw new DomainException("لا يمكن إلغاء طلب سحب يخص سكرتيرة أخرى.");
        }

        CancelledByUserId = actorUserId;
        CancelledAt = cancelledAt;
        Status = CashWithdrawalStatus.Cancelled;
    }

    public void Execute(long actorUserId, Guid executionIdempotencyKey,
        DateTimeOffset executedAt)
    {
        EnsureStatus(CashWithdrawalStatus.Approved,
            "طلب السحب غير موافق عليه أو تم تنفيذه بالفعل.");
        if (actorUserId != RequestedByUserId || executionIdempotencyKey == Guid.Empty)
        {
            throw new DomainException("بيانات تنفيذ السحب غير صحيحة.");
        }

        ExecutionIdempotencyKey = executionIdempotencyKey;
        ExecutedByUserId = actorUserId;
        ExecutedAt = executedAt;
        Status = CashWithdrawalStatus.Executed;
    }

    private void Review(long adminUserId, DateTimeOffset reviewedAt,
        string decisionReason, CashWithdrawalStatus status)
    {
        bool canReview = Status == CashWithdrawalStatus.Pending ||
            status == CashWithdrawalStatus.Rejected &&
            Status == CashWithdrawalStatus.Approved;
        if (!canReview)
        {
            throw new DomainException("تم حسم طلب السحب بالفعل.");
        }

        if (adminUserId <= 0 || adminUserId == RequestedByUserId)
        {
            throw new DomainException("مراجع طلب السحب غير صحيح.");
        }

        ReviewedByAdminUserId = adminUserId;
        ReviewedAt = reviewedAt;
        DecisionReason = CashierGuard.RequiredText(decisionReason,
            "سبب قرار الأدمن", 500);
        Status = status;
    }

    private void EnsureStatus(CashWithdrawalStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(message);
        }
    }
}
