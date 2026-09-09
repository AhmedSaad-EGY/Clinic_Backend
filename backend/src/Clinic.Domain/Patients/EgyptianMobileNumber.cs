using System.Globalization;
using Clinic.Domain.Common;

namespace Clinic.Domain.Patients;

public static class EgyptianMobileNumber
{
    public static string Normalize(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} مطلوب.");
        }

        string digits = NormalizeDigits(value);
        if (digits.StartsWith("0020", StringComparison.Ordinal))
        {
            digits = $"0{digits[4..]}";
        }
        else if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = $"0{digits[2..]}";
        }

        bool validPrefix = digits.StartsWith("010", StringComparison.Ordinal) ||
            digits.StartsWith("011", StringComparison.Ordinal) ||
            digits.StartsWith("012", StringComparison.Ordinal) ||
            digits.StartsWith("015", StringComparison.Ordinal);
        if (digits.Length != 11 || !validPrefix || digits.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new DomainException($"{fieldName} يجب أن يكون رقم موبايل مصريًا صحيحًا.");
        }

        return digits;
    }

    public static bool TryNormalize(string? value, out string? normalized)
    {
        try
        {
            normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : Normalize(value, "رقم الموبايل");
            return normalized is not null;
        }
        catch (DomainException)
        {
            normalized = null;
            return false;
        }
    }

    private static string NormalizeDigits(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;
        foreach (char character in value.Trim())
        {
            if (character is ' ' or '-' or '(' or ')' or '+')
            {
                continue;
            }

            double numericValue = char.GetNumericValue(character);
            buffer[length++] = numericValue is >= 0 and <= 9
                ? ((int)numericValue).ToString(CultureInfo.InvariantCulture)[0]
                : character;
        }

        return new string(buffer[..length]);
    }
}
