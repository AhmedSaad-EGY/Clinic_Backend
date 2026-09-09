using Clinic.Domain.Common;

namespace Clinic.Domain.Appointments;

internal static class AppointmentGuard
{
    public static void PositiveId(long value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field} غير صحيح.");
        }
    }

    public static void ValidRange(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (startAt >= endAt)
        {
            throw new DomainException("وقت نهاية الحجز يجب أن يكون بعد وقت البداية.");
        }
    }
}
