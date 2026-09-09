using Clinic.Domain.Common;

namespace Clinic.Domain.Scheduling;

public sealed class DoctorSchedule : AggregateRoot
{
    private DoctorSchedule()
    {
    }

    private DoctorSchedule(
        long doctorId,
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo)
    {
        Validate(doctorId, dayOfWeek, startTime, endTime, effectiveFrom, effectiveTo);
        DoctorId = doctorId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = true;
    }

    public long DoctorId { get; private set; }

    public Doctor Doctor { get; private set; } = null!;

    public ClinicDayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static DoctorSchedule Create(
        long doctorId,
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo) =>
        new(doctorId, dayOfWeek, startTime, endTime, effectiveFrom, effectiveTo);

    public void Update(
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo)
    {
        Validate(DoctorId, dayOfWeek, startTime, endTime, effectiveFrom, effectiveTo);
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public void Deactivate() => IsActive = false;

    private static void Validate(
        long doctorId,
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo)
    {
        SchedulingGuard.PositiveId(doctorId, "الطبيب");
        if (!Enum.IsDefined(dayOfWeek))
        {
            throw new DomainException("يوم الأسبوع غير صحيح.");
        }

        SchedulingGuard.ValidTimeRange(startTime, endTime);
        if (effectiveTo < effectiveFrom)
        {
            throw new DomainException("تاريخ نهاية الجدول يجب ألا يسبق تاريخ بدايته.");
        }
    }
}
