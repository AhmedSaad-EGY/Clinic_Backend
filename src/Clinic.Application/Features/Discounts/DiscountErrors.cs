using Clinic.Application.Common;

namespace Clinic.Application.Features.Discounts;

public static class DiscountErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "discounts.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError NotFound = new(
        "discounts.not_found", "الخصم غير موجود.");
    public static readonly ResultError TargetNotFound = new(
        "discounts.target_not_found", "أحد أهداف الخصم غير موجود أو غير متاح.");
    public static readonly ResultError Overlap = new(
        "discounts.overlap", "يوجد خصم آخر يغطي نفس الهدف خلال هذه الفترة.");
    public static readonly ResultError ConcurrencyConflict = new(
        "discounts.concurrency_conflict", "تم تعديل الخصم بواسطة مستخدم آخر. حدّث البيانات وحاول مجددًا.");
    public static ResultError Validation(string message) => new("discounts.validation", message);
}
