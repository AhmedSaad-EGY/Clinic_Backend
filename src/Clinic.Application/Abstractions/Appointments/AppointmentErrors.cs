namespace Clinic.Application.Abstractions.Appointments;

public static class AppointmentErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "appointments.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError NotFound = new(
        "appointments.not_found", "الحجز غير موجود.");
    public static readonly ResultError PatientNotFound = new(
        "appointments.patient_not_found", "ملف المريض غير موجود أو مؤرشف.");
    public static readonly ResultError DepartmentNotFound = new(
        "appointments.department_not_found", "القسم أو غرفته غير متاح.");
    public static readonly ResultError ResourceNotFound = new(
        "appointments.resource_not_found", "إحدى الخدمات أو الأطباء أو الأجهزة غير متاحة للحجز.");
    public static readonly ResultError FollowUpNotFound = new(
        "appointments.follow_up_not_found", "المتابعة غير موجودة أو لم تعد متاحة للحجز.");
    public static readonly ResultError ConcurrencyConflict = new(
        "appointments.concurrency_conflict", "تم تعديل الحجز بواسطة مستخدم آخر. حدّث الصفحة وحاول مجددًا.");
    public static readonly ResultError NotEditable = new(
        "appointments.not_editable", "لا يمكن تعديل الحجز في حالته الحالية.");

    public static ResultError Validation(string message) => new("appointments.validation", message);

    public static ResultError Conflict(string message, IReadOnlyCollection<AvailableSlot> alternatives) =>
        new("appointments.conflict", message,
            new Dictionary<string, object?> { ["alternatives"] = alternatives });
}
