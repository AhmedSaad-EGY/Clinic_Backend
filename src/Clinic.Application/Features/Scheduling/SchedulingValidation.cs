namespace Clinic.Application.Features.Scheduling;

internal static class SchedulingValidation
{
    public static Result<long> Actor(ICurrentUser currentUser) =>
        currentUser.UserId is > 0 and long id
            ? Result.Success(id)
            : Result.Failure<long>(SchedulingErrors.NotAuthenticated);

    public static Result<byte[]> RowVersion(string? value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value ?? string.Empty);
            return bytes.Length == 8
                ? Result.Success(bytes)
                : Result.Failure<byte[]>(SchedulingErrors.Validation("نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(SchedulingErrors.Validation("نسخة السجل غير صحيحة."));
        }
    }

    public static Result Required(string? value, string field, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure(SchedulingErrors.Validation($"{field} مطلوب."))
            : value.Trim().Length > maximumLength
                ? Result.Failure(SchedulingErrors.Validation($"{field} يتجاوز الطول المسموح."))
                : Result.Success();

    public static Result Optional(string? value, string field, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximumLength
            ? Result.Success()
            : Result.Failure(SchedulingErrors.Validation($"{field} يتجاوز الطول المسموح."));

    public static Result Schedule(
        ClinicDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo)
    {
        if (!Enum.IsDefined(dayOfWeek))
        {
            return Result.Failure(SchedulingErrors.Validation("يوم الأسبوع غير صحيح."));
        }

        if (startTime >= endTime)
        {
            return Result.Failure(SchedulingErrors.Validation(
                "وقت البداية يجب أن يسبق وقت النهاية."));
        }

        return effectiveTo < effectiveFrom
            ? Result.Failure(SchedulingErrors.Validation(
                "تاريخ نهاية الجدول يجب ألا يسبق تاريخ بدايته."))
            : Result.Success();
    }

    public static Result DoctorException(
        TimeOnly? startTime,
        TimeOnly? endTime,
        DoctorExceptionType type)
    {
        if (!Enum.IsDefined(type))
        {
            return Result.Failure(SchedulingErrors.Validation(
                "نوع الاستثناء غير صحيح."));
        }

        if (startTime.HasValue != endTime.HasValue)
        {
            return Result.Failure(SchedulingErrors.Validation(
                "يجب إدخال وقتي البداية والنهاية معًا أو تركهما معًا."));
        }

        return startTime.HasValue && startTime.Value >= endTime!.Value
            ? Result.Failure(SchedulingErrors.Validation(
                "وقت البداية يجب أن يسبق وقت النهاية."))
            : Result.Success();
    }

    public static Result Closure(DateTimeOffset startAt, DateTimeOffset endAt) =>
        startAt >= endAt
            ? Result.Failure(SchedulingErrors.Validation(
                "بداية إيقاف القسم يجب أن تسبق نهايته."))
            : Result.Success();

    public static Result Page(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100
            ? Result.Success()
            : Result.Failure(SchedulingErrors.Validation(
                "رقم الصفحة يجب أن يبدأ من 1 وحجم الصفحة يجب أن يكون بين 1 و100."));
}
