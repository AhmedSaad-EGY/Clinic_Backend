using System.Globalization;
using System.Text;

namespace Clinic.Infrastructure.Identity;

internal static class PhoneNumberNormalizer
{
    public static string? Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        StringBuilder normalized = new(phoneNumber.Length);

        foreach (char character in phoneNumber.Trim())
        {
            if (character == '+' && normalized.Length == 0)
            {
                normalized.Append(character);
                continue;
            }

            if (!char.IsDigit(character))
            {
                continue;
            }

            int digit = (int)char.GetNumericValue(character);
            normalized.Append(digit.ToString(CultureInfo.InvariantCulture));
        }

        if (normalized.Length == 0)
        {
            return null;
        }

        int digitCount = normalized[0] == '+' ? normalized.Length - 1 : normalized.Length;
        return digitCount is >= 7 and <= 15 ? normalized.ToString() : null;
    }
}
