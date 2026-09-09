using Clinic.Domain.Common;

namespace Clinic.Domain.Catalog;

internal static class CatalogGuard
{
    public static string RequiredText(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} مطلوب.");
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{fieldName} يتجاوز الطول المسموح.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{fieldName} يتجاوز الطول المسموح.");
        }

        return normalized;
    }

    public static void PositiveIdentifier(long value, string fieldName)
    {
        if (value <= 0)
        {
            throw new DomainException($"{fieldName} غير صحيح.");
        }
    }

    public static void PositiveMoney(decimal value)
    {
        if (value <= 0)
        {
            throw new DomainException("السعر يجب أن يكون أكبر من صفر.");
        }
    }
}
