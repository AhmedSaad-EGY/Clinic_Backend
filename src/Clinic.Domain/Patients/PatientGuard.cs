using Clinic.Domain.Common;

namespace Clinic.Domain.Patients;

internal static class PatientGuard
{
    public static string RequiredText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} مطلوب.");
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} يجب ألا يتجاوز {maxLength} حرفًا.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} يجب ألا يتجاوز {maxLength} حرفًا.");
        }

        return normalized;
    }

    public static void PositiveId(long value, string fieldName)
    {
        if (value <= 0)
        {
            throw new DomainException($"معرف {fieldName} غير صحيح.");
        }
    }
}
