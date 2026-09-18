namespace Clinic.Domain.Cashier;

public sealed class Shift : AggregateRoot
{
    private Shift()
    {
    }

    private Shift(long cashDrawerId, DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd, int closingGraceMinutes,
        long scheduledByAdminUserId)
    {
        CashierGuard.PositiveId(cashDrawerId, "درج الكاش");
        CashierGuard.PositiveId(scheduledByAdminUserId, "الأدمن");
        ValidateSchedule(scheduledStart, scheduledEnd, closingGraceMinutes);
        CashDrawerId = cashDrawerId;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
        GraceEndsAt = scheduledEnd.AddMinutes(closingGraceMinutes);
        Status = ShiftStatus.Scheduled;
        ScheduledByAdminUserId = scheduledByAdminUserId;
    }

    public long CashDrawerId { get; private set; }

    public CashDrawer CashDrawer { get; private set; } = null!;

    public DateTimeOffset ScheduledStart { get; private set; }

    public DateTimeOffset ScheduledEnd { get; private set; }

    public DateTimeOffset GraceEndsAt { get; private set; }

    public DateTimeOffset? ActualOpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public decimal? OpeningBalance { get; private set; }

    public long? OpeningBalanceEnteredByUserId { get; private set; }

    public DateTimeOffset? OpeningBalanceEnteredAt { get; private set; }

    public decimal? ExpectedCash { get; private set; }

    public decimal? DeclaredCash { get; private set; }

    public decimal? CashVariance { get; private set; }

    public ShiftStatus Status { get; private set; }

    public bool OpenedAutomatically { get; private set; }

    public long ScheduledByAdminUserId { get; private set; }

    public long? ClosedByUserId { get; private set; }

    public string? CloseNote { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public long? CancelledByAdminUserId { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Shift Create(long cashDrawerId, DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd, int closingGraceMinutes,
        long scheduledByAdminUserId) => new(cashDrawerId, scheduledStart,
            scheduledEnd, closingGraceMinutes, scheduledByAdminUserId);

    public ShiftStatus GetEffectiveStatus(DateTimeOffset now)
    {
        if (Status is ShiftStatus.Closed or ShiftStatus.Cancelled)
        {
            return Status;
        }

        return now < ScheduledStart
            ? ShiftStatus.Scheduled
            : now < ScheduledEnd ? ShiftStatus.Open : ShiftStatus.Grace;
    }

    public bool CanCollect(DateTimeOffset now) =>
        OpeningBalance.HasValue &&
        Status is not ShiftStatus.Closed and not ShiftStatus.Cancelled &&
        now >= ScheduledStart && now < GraceEndsAt;

    public void UpdateSchedule(DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd, int closingGraceMinutes, DateTimeOffset now)
    {
        EnsureFuture(now);
        ValidateSchedule(scheduledStart, scheduledEnd, closingGraceMinutes);
        if (scheduledStart <= now)
        {
            throw new DomainException("يجب أن يبدأ الشيفت المعدل في وقت لاحق.");
        }

        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
        GraceEndsAt = scheduledEnd.AddMinutes(closingGraceMinutes);
    }

    public void Extend(DateTimeOffset newScheduledEnd, long adminUserId,
        DateTimeOffset now)
    {
        EnsureActive();
        CashierGuard.PositiveId(adminUserId, "الأدمن");
        if (now < ScheduledStart || newScheduledEnd <= ScheduledEnd ||
            newScheduledEnd <= now)
        {
            throw new DomainException("وقت نهاية الشيفت الجديد غير صحيح.");
        }

        TimeSpan grace = GraceEndsAt - ScheduledEnd;
        ScheduledEnd = newScheduledEnd;
        GraceEndsAt = newScheduledEnd.Add(grace);
        OpenForInteraction(now);
        ClearReconciliation();
    }

    public void Cancel(long adminUserId, DateTimeOffset cancelledAt, string reason)
    {
        EnsureFuture(cancelledAt);
        CashierGuard.PositiveId(adminUserId, "الأدمن");
        Status = ShiftStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancelledByAdminUserId = adminUserId;
        CancellationReason = CashierGuard.RequiredText(reason, "سبب الإلغاء", 500);
    }

    public void RecordOpeningBalance(decimal amount, long actorUserId,
        DateTimeOffset occurredAt)
    {
        EnsureStartedAndActive(occurredAt);
        CashierGuard.PositiveId(actorUserId, "المستخدم");
        CashierGuard.NonNegativeMoney(amount, "رصيد البداية");
        if (OpeningBalance.HasValue)
        {
            throw new DomainException("تم تسجيل رصيد بداية الشيفت بالفعل.");
        }

        OpenForInteraction(occurredAt);
        OpeningBalance = amount;
        OpeningBalanceEnteredByUserId = actorUserId;
        OpeningBalanceEnteredAt = occurredAt;
    }

    public void Reconcile(decimal expectedCash, decimal declaredCash,
        long actorUserId, DateTimeOffset occurredAt)
    {
        EnsureStartedAndActive(occurredAt);
        EnsureOpeningBalance();
        CashierGuard.PositiveId(actorUserId, "المستخدم");
        CashierGuard.NonNegativeMoney(declaredCash, "المبلغ الفعلي");
        OpenForInteraction(occurredAt);
        ExpectedCash = expectedCash;
        DeclaredCash = declaredCash;
        CashVariance = declaredCash - expectedCash;
    }

    public void RegisterCollection(DateTimeOffset collectedAt)
    {
        if (!CanCollect(collectedAt))
        {
            throw new DomainException("لا يمكن تسجيل تحصيل في هذا الشيفت الآن.");
        }

        OpenForInteraction(collectedAt);
        ClearReconciliation();
    }

    public void RegisterRefund(DateTimeOffset refundedAt)
    {
        if (!CanCollect(refundedAt))
        {
            throw new DomainException("لا يمكن تسجيل استرداد في هذا الشيفت الآن.");
        }

        OpenForInteraction(refundedAt);
        ClearReconciliation();
    }

    public void RegisterCashWithdrawal(DateTimeOffset executedAt)
    {
        if (!CanCollect(executedAt))
        {
            throw new DomainException("لا يمكن تنفيذ سحب نقدي في هذا الشيفت الآن.");
        }

        OpenForInteraction(executedAt);
        ClearReconciliation();
    }

    public void Close(decimal currentExpectedCash, long actorUserId,
        DateTimeOffset closedAt, bool allowEarlyClose, string? note)
    {
        EnsureStartedAndActive(closedAt);
        EnsureOpeningBalance();
        CashierGuard.PositiveId(actorUserId, "المستخدم");
        if (!allowEarlyClose && closedAt < ScheduledEnd)
        {
            throw new DomainException("لا يمكن للسكرتيرة إغلاق الشيفت قبل موعد نهايته.");
        }

        if (!DeclaredCash.HasValue || ExpectedCash != currentExpectedCash)
        {
            throw new DomainException("يجب إجراء مطابقة جديدة قبل إغلاق الشيفت.");
        }

        decimal variance = DeclaredCash.Value - currentExpectedCash;
        CloseNote = variance == 0
            ? CashierGuard.OptionalText(note, 500)
            : CashierGuard.RequiredText(note ?? string.Empty, "سبب فرق الكاش", 500);
        Status = ShiftStatus.Closed;
        ClosedAt = closedAt;
        ClosedByUserId = actorUserId;
        CashVariance = variance;
    }

    private void OpenForInteraction(DateTimeOffset occurredAt)
    {
        ActualOpenedAt ??= occurredAt;
        OpenedAutomatically = true;
        Status = GetEffectiveStatus(occurredAt);
    }

    private void EnsureFuture(DateTimeOffset now)
    {
        EnsureActive();
        if (now >= ScheduledStart || Status != ShiftStatus.Scheduled)
        {
            throw new DomainException("لا يمكن تعديل أو إلغاء شيفت بدأ بالفعل.");
        }
    }

    private void EnsureStartedAndActive(DateTimeOffset now)
    {
        EnsureActive();
        if (now < ScheduledStart)
        {
            throw new DomainException("لم يبدأ موعد الشيفت بعد.");
        }
    }

    private void EnsureActive()
    {
        if (Status is ShiftStatus.Closed or ShiftStatus.Cancelled)
        {
            throw new DomainException("الشيفت مغلق أو ملغي ولا يمكن تعديله.");
        }
    }

    private void EnsureOpeningBalance()
    {
        if (!OpeningBalance.HasValue)
        {
            throw new DomainException("يجب تسجيل رصيد البداية أولًا.");
        }
    }

    private void ClearReconciliation()
    {
        ExpectedCash = null;
        DeclaredCash = null;
        CashVariance = null;
    }

    private static void ValidateSchedule(DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd, int closingGraceMinutes)
    {
        if (scheduledStart >= scheduledEnd)
        {
            throw new DomainException("وقت نهاية الشيفت يجب أن يكون بعد وقت بدايته.");
        }

        CashierGuard.GraceMinutes(closingGraceMinutes);
    }
}
