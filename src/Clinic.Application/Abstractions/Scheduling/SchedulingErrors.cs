namespace Clinic.Application.Abstractions.Scheduling;

public static class SchedulingErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "scheduling.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError DoctorNotFound = new(
        "scheduling.doctor_not_found", "الطبيب غير موجود.");
    public static readonly ResultError ScheduleNotFound = new(
        "scheduling.schedule_not_found", "فترة العمل غير موجودة.");
    public static readonly ResultError ExceptionNotFound = new(
        "scheduling.exception_not_found", "استثناء الطبيب غير موجود.");
    public static readonly ResultError ClosureNotFound = new(
        "scheduling.closure_not_found", "فترة إيقاف القسم غير موجودة.");
    public static readonly ResultError DepartmentNotFound = new(
        "scheduling.department_not_found", "القسم غير موجود.");
    public static readonly ResultError ServiceNotFound = new(
        "scheduling.service_not_found", "خدمة واحدة أو أكثر غير موجودة أو غير فعالة.");
    public static readonly ResultError ConcurrencyConflict = new(
        "scheduling.concurrency_conflict",
        "تم تعديل البيانات بواسطة مستخدم آخر. حدّث الصفحة ثم حاول مرة أخرى.");
    public static ResultError Validation(string message) => new("scheduling.validation", message);
    public static ResultError Conflict(string message) => new("scheduling.conflict", message);
}
