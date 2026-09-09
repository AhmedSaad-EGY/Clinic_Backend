using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;

namespace Clinic.Application.Features.Catalog;

internal static class CatalogCommandValidation
{
    public static Result<long> GetActor(ICurrentUser currentUser) =>
        currentUser.UserId is long actorUserId && actorUserId > 0
            ? Result.Success(actorUserId)
            : Result.Failure<long>(CatalogErrors.NotAuthenticated);

    public static Result<byte[]> DecodeRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return Result.Failure<byte[]>(CatalogErrors.Validation(
                "نسخة السجل مطلوبة."));
        }

        try
        {
            byte[] value = Convert.FromBase64String(rowVersion);
            return value.Length == 8
                ? Result.Success(value)
                : Result.Failure<byte[]>(CatalogErrors.Validation(
                    "نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(CatalogErrors.Validation(
                "نسخة السجل غير صحيحة."));
        }
    }

    public static Result ValidateName(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure(CatalogErrors.Validation($"{fieldName} مطلوب."));
        }

        return value.Trim().Length <= maximumLength
            ? Result.Success()
            : Result.Failure(CatalogErrors.Validation(
                $"{fieldName} يتجاوز الطول المسموح."));
    }

    public static Result ValidateOptionalText(
        string? value,
        string fieldName,
        int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximumLength
            ? Result.Success()
            : Result.Failure(CatalogErrors.Validation(
                $"{fieldName} يتجاوز الطول المسموح."));

    public static Result ValidatePage(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100
            ? Result.Success()
            : Result.Failure(CatalogErrors.Validation(
                "رقم الصفحة يجب أن يبدأ من 1 وحجم الصفحة يجب أن يكون بين 1 و100."));
}
