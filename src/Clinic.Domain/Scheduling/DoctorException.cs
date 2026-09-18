namespace Clinic.Domain.Scheduling;

public sealed class DoctorScheduleOverride : AggregateRoot
{
    private DoctorScheduleOverride()
    {
    }

    private DoctorScheduleOverride(
        long doctorId,
        DateOnly exceptionDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        DoctorExceptionType type,
        string? reason,
        long createdByUserId,
        DateTimeOffset createdAt)
    {
        Validate(doctorId, startTime, endTime, type, createdByUserId);
        DoctorId = doctorId;
        ExceptionDate = exceptionDate;
        StartTime = startTime;
        EndTime = endTime;
        Type = type;
        Reason = SchedulingGuard.OptionalText(reason, "سبب الاستثناء", 500);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long DoctorId { get; private set; }

    public Doctor Doctor { get; private set; } = null!;

    public DateOnly ExceptionDate { get; private set; }

    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public DoctorExceptionType Type { get; private set; }

    public string? Reason { get; private set; }

    public long CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public long? CancelledByUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsCancelled => CancelledAt.HasValue;

    public static DoctorScheduleOverride Create(
        long doctorId,
        DateOnly exceptionDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        DoctorExceptionType type,
        string? reason,
        long createdByUserId,
        DateTimeOffset createdAt) =>
        new(doctorId, exceptionDate, startTime, endTime, type, reason, createdByUserId, createdAt);

    public void Cancel(long cancelledByUserId, DateTimeOffset cancelledAt)
    {
        SchedulingGuard.PositiveId(cancelledByUserId, "المستخدم");
        if (IsCancelled)
        {
            throw new DomainException("تم إلغاء استثناء الطبيب بالفعل.");
        }

        CancelledByUserId = cancelledByUserId;
        CancelledAt = cancelledAt;
    }

    private static void Validate(
        long doctorId,
        TimeOnly? startTime,
        TimeOnly? endTime,
        DoctorExceptionType type,
        long createdByUserId)
    {
        SchedulingGuard.PositiveId(doctorId, "الطبيب");
        SchedulingGuard.PositiveId(createdByUserId, "المستخدم");
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("نوع الاستثناء غير صحيح.");
        }

        if (startTime.HasValue != endTime.HasValue)
        {
            throw new DomainException("يجب إدخال وقتي البداية والنهاية معًا أو تركهما معًا.");
        }

        if (startTime.HasValue)
        {
            SchedulingGuard.ValidTimeRange(startTime.Value, endTime!.Value);
        }
    }
}
