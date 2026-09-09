using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Domain.Cashier;

namespace Clinic.Application.Features.Cashier;

internal static class CashierValidation
{
    public static Result<long> Actor(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated && currentUser.UserId is > 0
            ? Result.Success(currentUser.UserId.Value)
            : Result.Failure<long>(CashierErrors.NotAuthenticated);

    public static Result<byte[]> RowVersion(string value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value);
            return bytes.Length == 8
                ? Result.Success(bytes)
                : Result.Failure<byte[]>(CashierErrors.Validation(
                    "نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(CashierErrors.Validation(
                "نسخة السجل غير صحيحة."));
        }
    }

    public static Result Generation(GenerateShiftsInput input)
    {
        if (input.SecretaryUserId <= 0 || input.FromDate > input.ToDate ||
            input.ToDate.DayNumber - input.FromDate.DayNumber > 89 ||
            input.DaysOfWeek.Count == 0 ||
            input.DaysOfWeek.Any(day => !Enum.IsDefined(day)) ||
            input.DaysOfWeek.Distinct().Count() != input.DaysOfWeek.Count ||
            input.StartTime >= input.EndTime)
        {
            return Result.Failure(CashierErrors.Validation(
                "بيانات توليد الشيفتات غير صحيحة أو تتجاوز 90 يومًا."));
        }

        return Result.Success();
    }

    public static Result Update(UpdateShiftInput input) =>
        input.StartTime < input.EndTime
            ? Result.Success()
            : Result.Failure(CashierErrors.Validation(
                "وقت نهاية الشيفت يجب أن يكون بعد وقت بدايته في اليوم نفسه."));

    public static Result Money(decimal amount) => amount >= 0
        ? Result.Success()
        : Result.Failure(CashierErrors.Validation("المبلغ لا يمكن أن يكون سالبًا."));

    public static Result Reason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500
            ? Result.Success()
            : Result.Failure(CashierErrors.Validation(
                "السبب مطلوب ويجب ألا يتجاوز 500 حرف."));

    public static Result Page(int pageNumber, int pageSize) =>
        pageNumber > 0 && pageSize is > 0 and <= 100
            ? Result.Success()
            : Result.Failure(CashierErrors.Validation("بيانات الصفحة غير صحيحة."));

    public static Result Search(ShiftSearch search) =>
        search.FromDate > search.ToDate || search.ToDate == DateOnly.MaxValue ||
        (search.Status.HasValue && !Enum.IsDefined(search.Status.Value))
            ? Result.Failure(CashierErrors.Validation("معايير البحث غير صحيحة."))
            : Page(search.PageNumber, search.PageSize);

    public static Result Payment(PostPaymentInput input, Guid idempotencyKey,
        bool adminOverride)
    {
        if (idempotencyKey == Guid.Empty || input.AppointmentIds.Count is < 1 or > 20 ||
            input.AppointmentIds.Any(id => id <= 0) ||
            input.AppointmentIds.Distinct().Count() != input.AppointmentIds.Count ||
            input.MethodAllocations.Count is < 1 or > 4 ||
            input.MethodAllocations.Any(item => item.PaymentMethodId <= 0 ||
                item.Amount <= 0 || decimal.Round(item.Amount, 2) != item.Amount ||
                item.ReferenceNumber?.Trim().Length > 100) ||
            input.MethodAllocations.Select(item => item.PaymentMethodId).Distinct().Count() !=
                input.MethodAllocations.Count || input.Note?.Trim().Length > 500)
        {
            return Result.Failure(CashierErrors.Validation(
                "بيانات عملية التحصيل غير صحيحة."));
        }

        if (adminOverride)
        {
            return input.ShiftId is > 0
                ? Reason(input.Reason)
                : Result.Failure(CashierErrors.Validation("الشيفت مطلوب لتحصيل الأدمن."));
        }

        return input.ShiftId.HasValue || !string.IsNullOrWhiteSpace(input.Reason)
            ? Result.Failure(CashierErrors.Validation(
                "لا تحدد السكرتيرة الشيفت أو سبب تدخل الأدمن."))
            : Result.Success();
    }

    public static Result ApprovalSearch(ApprovalRequestSearch search) =>
        search.Status.HasValue && !Enum.IsDefined(search.Status.Value)
            ? Result.Failure(CashierErrors.Validation("حالة طلب الموافقة غير صحيحة."))
            : Page(search.PageNumber, search.PageSize);

    public static Result Refund(ExecuteRefundInput input, Guid idempotencyKey)
    {
        if (idempotencyKey == Guid.Empty || input.ApprovalRequestId <= 0 ||
            input.MethodAllocations.Count is < 1 or > 4 ||
            input.MethodAllocations.Any(item => item.OriginalAllocationId <= 0 ||
                item.Amount <= 0 || decimal.Round(item.Amount, 2) != item.Amount ||
                item.ReferenceNumber?.Trim().Length > 100) ||
            input.MethodAllocations.Select(item => item.OriginalAllocationId)
                .Distinct().Count() != input.MethodAllocations.Count ||
            input.Note?.Trim().Length > 500)
        {
            return Result.Failure(CashierErrors.Validation(
                "بيانات عملية الاسترداد غير صحيحة."));
        }

        return Result.Success();
    }
}
