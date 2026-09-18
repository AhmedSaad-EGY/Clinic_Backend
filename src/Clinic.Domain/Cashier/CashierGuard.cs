namespace Clinic.Domain.Cashier;

internal static class CashierGuard
{
    public static void PositiveId(long value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"معرف {field} غير صحيح.");
        }
    }

    public static void GraceMinutes(int value)
    {
        if (value is < 0 or > ShiftPolicy.MaximumClosingGraceMinutes)
        {
            throw new DomainException("مهلة الإغلاق يجب أن تكون بين صفر و120 دقيقة.");
        }
    }

    public static void NonNegativeMoney(decimal value, string field)
    {
        if (value < 0)
        {
            throw new DomainException($"{field} لا يمكن أن يكون سالبًا.");
        }
    }

    public static void PositiveMoney(decimal value, string field)
    {
        if (value <= 0 || decimal.Round(value, 2) != value)
        {
            throw new DomainException($"{field} يجب أن يكون أكبر من صفر وبحد أقصى منزلتين عشريتين.");
        }
    }

    public static string RequiredText(string value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} مطلوب.");
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{field} أطول من الحد المسموح.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException("النص أطول من الحد المسموح.");
        }

        return normalized;
    }
}
