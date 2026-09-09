using System.Globalization;
using System.Net.Mail;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Patients;

namespace Clinic.Application.Features.Patients;

internal static class PatientValidation
{
    public static Result<long> Actor(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated && currentUser.UserId is > 0
            ? Result.Success(currentUser.UserId.Value)
            : Result.Failure<long>(PatientErrors.NotAuthenticated);

    public static Result<PatientInput> Input(PatientInput input)
    {
        try
        {
            Result fullName = RequiredText(input.FullName, "اسم المريض", 200);
            if (fullName.IsFailure)
            {
                return Result.Failure<PatientInput>(fullName.Error);
            }

            Result optionalFields = ValidateOptionalFields(input);
            if (optionalFields.IsFailure)
            {
                return Result.Failure<PatientInput>(optionalFields.Error);
            }

            if ((input.BirthDate is null) == (input.Age is null))
            {
                return Result.Failure<PatientInput>(PatientErrors.Validation(
                    "يجب إدخال تاريخ الميلاد أو العمر، وليس كليهما."));
            }

            if (input.Age is < 0 or > 130)
            {
                return Result.Failure<PatientInput>(PatientErrors.Validation(
                    "العمر يجب أن يكون بين 0 و130 سنة."));
            }

            if (!Enum.IsDefined(input.Gender))
            {
                return Result.Failure<PatientInput>(PatientErrors.Validation(
                    "نوع المريض غير صحيح."));
            }

            string primary = EgyptianMobileNumber.Normalize(input.PrimaryPhoneNumber,
                "رقم الموبايل الأساسي");
            string? secondary = string.IsNullOrWhiteSpace(input.SecondaryPhoneNumber)
                ? null
                : EgyptianMobileNumber.Normalize(input.SecondaryPhoneNumber,
                    "رقم الموبايل الإضافي");
            string? guardianPhone = string.IsNullOrWhiteSpace(input.GuardianPhoneNumber)
                ? null
                : EgyptianMobileNumber.Normalize(input.GuardianPhoneNumber,
                    "رقم موبايل ولي الأمر");
            if (secondary == primary)
            {
                return Result.Failure<PatientInput>(PatientErrors.Validation(
                    "رقم الموبايل الإضافي يجب أن يختلف عن الرقم الأساسي."));
            }

            return Result.Success(input with
            {
                PrimaryPhoneNumber = primary,
                SecondaryPhoneNumber = secondary,
                GuardianPhoneNumber = guardianPhone
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PatientInput>(PatientErrors.Validation(exception.Message));
        }
    }

    public static Result<byte[]> RowVersion(string rowVersion)
    {
        try
        {
            byte[] decoded = Convert.FromBase64String(rowVersion);
            return decoded.Length == 8
                ? Result.Success(decoded)
                : Result.Failure<byte[]>(PatientErrors.Validation("قيمة إصدار السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(PatientErrors.Validation("قيمة إصدار السجل غير صحيحة."));
        }
    }

    public static Result RequiredText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure(PatientErrors.Validation($"{fieldName} مطلوب."));
        }

        return value.Trim().Length <= maxLength
            ? Result.Success()
            : Result.Failure(PatientErrors.Validation(
                $"{fieldName} يجب ألا يتجاوز {maxLength} حرفًا."));
    }

    public static Result Page(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100 &&
        (long)(pageNumber - 1) * pageSize <= int.MaxValue
            ? Result.Success()
            : Result.Failure(PatientErrors.Validation(
                "رقم الصفحة غير صحيح أو حجم الصفحة ليس بين 1 و100."));

    public static long? ParseFileNumber(string? search) =>
        long.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value > 0
            ? value
            : null;

    private static Result ValidateOptionalFields(PatientInput input)
    {
        (string? Value, string Name, int MaxLength)[] fields =
        [
            (input.Area, "المنطقة", 150),
            (input.Address, "العنوان", 500),
            (input.Email, "البريد الإلكتروني", 256),
            (input.GuardianName, "اسم ولي الأمر", 200),
        ];
        foreach ((string? value, string name, int maxLength) in fields)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
            {
                return Result.Failure(PatientErrors.Validation(
                    $"{name} يجب ألا يتجاوز {maxLength} حرفًا."));
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Email) &&
            !MailAddress.TryCreate(input.Email.Trim(), out _))
        {
            return Result.Failure(PatientErrors.Validation("البريد الإلكتروني غير صحيح."));
        }

        return Result.Success();
    }
}
