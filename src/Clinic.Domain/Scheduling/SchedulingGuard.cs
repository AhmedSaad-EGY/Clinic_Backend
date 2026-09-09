using Clinic.Domain.Common;

namespace Clinic.Domain.Scheduling;

internal static class SchedulingGuard
{
    public static void PositiveId(long value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field} غير صحيح.");
        }
    }

    public static string RequiredText(string? value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} مطلوب.");
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{field} يجب ألا يتجاوز {maximumLength} حرفًا.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{field} يجب ألا يتجاوز {maximumLength} حرفًا.");
        }

        return normalized;
    }

    public static void ValidTimeRange(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime >= endTime)
        {
            throw new DomainException("وقت البداية يجب أن يسبق وقت النهاية.");
        }
    }
}
