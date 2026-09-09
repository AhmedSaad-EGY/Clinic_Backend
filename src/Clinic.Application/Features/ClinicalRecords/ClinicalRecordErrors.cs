using Clinic.Application.Common;

namespace Clinic.Application.Features.ClinicalRecords;

public static class ClinicalRecordErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "clinical.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError PrescriptionNotFound = new(
        "clinical.prescription_not_found", "الروشتة غير موجودة.");
    public static readonly ResultError AppointmentServiceNotFound = new(
        "clinical.appointment_service_not_found",
        "خدمة الحجز غير موجودة أو لم تكتمل بعد.");
    public static readonly ResultError FollowUpNotFound = new(
        "clinical.follow_up_not_found", "المتابعة غير موجودة.");
    public static readonly ResultError PatientNotFound = new(
        "clinical.patient_not_found", "ملف المريض غير موجود.");
    public static readonly ResultError ConcurrencyConflict = new(
        "clinical.concurrency_conflict",
        "تم تعديل السجل بواسطة مستخدم آخر. حدّث الصفحة وحاول مرة أخرى.");
    public static ResultError Validation(string message) =>
        new("clinical.validation", message);
    public static ResultError Conflict(string message) =>
        new("clinical.conflict", message);
}
