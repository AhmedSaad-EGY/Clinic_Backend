namespace Clinic.Domain.Scheduling;

public sealed class DepartmentClosure : AggregateRoot
{
    private DepartmentClosure()
    {
    }

    private DepartmentClosure(
        long departmentId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string reason,
        long createdByUserId,
        DateTimeOffset createdAt)
    {
        SchedulingGuard.PositiveId(departmentId, "القسم");
        SchedulingGuard.PositiveId(createdByUserId, "المستخدم");
        if (startAt >= endAt)
        {
            throw new DomainException("بداية إيقاف القسم يجب أن تسبق نهايته.");
        }

        DepartmentId = departmentId;
        StartAt = startAt.ToUniversalTime();
        EndAt = endAt.ToUniversalTime();
        Reason = SchedulingGuard.RequiredText(reason, "سبب إيقاف القسم", 500);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public long CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public long? CancelledByUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsCancelled => CancelledAt.HasValue;

    public static DepartmentClosure Create(
        long departmentId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string reason,
        long createdByUserId,
        DateTimeOffset createdAt) =>
        new(departmentId, startAt, endAt, reason, createdByUserId, createdAt);

    public void Cancel(long cancelledByUserId, DateTimeOffset cancelledAt)
    {
        SchedulingGuard.PositiveId(cancelledByUserId, "المستخدم");
        if (IsCancelled)
        {
            throw new DomainException("تم إلغاء فترة إيقاف القسم بالفعل.");
        }

        CancelledByUserId = cancelledByUserId;
        CancelledAt = cancelledAt.ToUniversalTime();
    }
}
