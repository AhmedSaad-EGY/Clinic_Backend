using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;

namespace Clinic.Application.Features.Appointments;

internal static class AppointmentValidation
{
    public static Result<long> Actor(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated && currentUser.UserId is > 0
            ? Result.Success(currentUser.UserId.Value)
            : Result.Failure<long>(AppointmentErrors.NotAuthenticated);

    public static Result Input(AppointmentInput input)
    {
        if (input.PatientId <= 0 || input.DepartmentId <= 0 || input.Services.Count == 0)
        {
            return Result.Failure(AppointmentErrors.Validation("بيانات الحجز غير مكتملة."));
        }

        if (input.PatientPackageId is <= 0 ||
            input.PatientPackageId.HasValue &&
            (input.FollowUp is not null || input.Services.Any(item => item.Quantity != 1) ||
             input.Services.Select(item => item.ServiceId).Distinct().Count() !=
             input.Services.Count))
        {
            return Result.Failure(AppointmentErrors.Validation(
                "حجز الباقة يحتاج مفتاح طلب صالح وخدمات غير مكررة بكمية واحدة."));
        }

        if (input.StartAt.Ticks % TimeSpan.TicksPerMinute != 0 ||
            input.StartAt.Minute % 15 != 0)
        {
            return Result.Failure(AppointmentErrors.Validation(
                "وقت بداية الحجز يجب أن يكون على فاصل 15 دقيقة."));
        }

        if (input.Services.Any(item => item.ServiceId <= 0 || item.DoctorId <= 0 ||
            item.Quantity <= 0 || item.OptionalDeviceIds.Any(id => id <= 0) ||
            item.OptionalDeviceIds.Count != item.OptionalDeviceIds.Distinct().Count()))
        {
            return Result.Failure(AppointmentErrors.Validation("بيانات خدمات الحجز غير صحيحة."));
        }

        if (input.FollowUp is not null && (input.FollowUp.FollowUpId <= 0 ||
            RowVersion(input.FollowUp.RowVersion).IsFailure))
        {
            return Result.Failure(AppointmentErrors.Validation(
                "بيانات المتابعة المراد تحويلها إلى حجز غير صحيحة."));
        }

        return Result.Success();
    }

    public static Result<byte[]> RowVersion(string value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value);
            return bytes.Length == 8
                ? Result.Success(bytes)
                : Result.Failure<byte[]>(AppointmentErrors.Validation("نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(AppointmentErrors.Validation("نسخة السجل غير صحيحة."));
        }
    }
}
