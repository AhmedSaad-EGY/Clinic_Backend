using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;

namespace Clinic.Application.Features.ClinicalRecords;

public static class ClinicalRecordValidation
{
    public static Result<long> Actor(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated && currentUser.UserId is > 0
            ? Result.Success(currentUser.UserId.Value)
            : Result.Failure<long>(ClinicalRecordErrors.NotAuthenticated);

    public static Result Content(PrescriptionContentInput content)
    {
        if (content.Items.Count > 50 ||
            content.ReturnDate.HasValue && content.ReturnAfterDays.HasValue ||
            content.ReturnAfterDays <= 0 || content.Items.Any(InvalidItem))
        {
            return Result.Failure(ClinicalRecordErrors.Validation(
                "بيانات الروشتة أو موعد الإعادة غير صحيحة."));
        }
        return Result.Success();
    }

    public static Result<byte[]> RowVersion(string value)
    {
        try
        {
            byte[] version = Convert.FromBase64String(value);
            return version.Length == 8 ? Result.Success(version) : InvalidVersion();
        }
        catch (FormatException) { return InvalidVersion(); }
    }

    public static Result Page(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100 &&
        (long)(pageNumber - 1) * pageSize <= int.MaxValue
            ? Result.Success()
            : Result.Failure(ClinicalRecordErrors.Validation(
                "رقم الصفحة غير صحيح أو حجم الصفحة ليس بين 1 و100."));

    public static Result RequiredReason(string reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500
            ? Result.Success()
            : Result.Failure(ClinicalRecordErrors.Validation(
                "السبب مطلوب ويجب ألا يتجاوز 500 حرف."));

    private static bool InvalidItem(PrescriptionItemInput item) =>
        string.IsNullOrWhiteSpace(item.MedicineName) ||
        item.MedicineName.Trim().Length > 200 || item.DoseAmount <= 0 ||
        item.DoseAmount.HasValue && decimal.Round(item.DoseAmount.Value, 4) != item.DoseAmount ||
        item.DoseUnit?.Trim().Length > 50 || item.TimesPerDay <= 0 ||
        item.FrequencyText?.Trim().Length > 200 ||
        item.DurationText?.Trim().Length > 200 ||
        item.Instructions?.Trim().Length > 1000 ||
        item.FoodTiming.HasValue && !Enum.IsDefined(item.FoodTiming.Value);

    private static Result<byte[]> InvalidVersion() => Result.Failure<byte[]>(
        ClinicalRecordErrors.Validation("قيمة إصدار السجل غير صحيحة."));
}
