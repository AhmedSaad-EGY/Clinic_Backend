using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Patients;

public static class PatientErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "patients.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError PatientNotFound = new(
        "patients.patient_not_found", "ملف المريض غير موجود.");
    public static readonly ResultError NoteNotFound = new(
        "patients.note_not_found", "الملاحظة غير موجودة.");
    public static readonly ResultError TreatmentHistoryNotFound = new(
        "patients.treatment_history_not_found", "سجل التاريخ العلاجي غير موجود.");
    public static readonly ResultError DuplicatePrimaryPhone = new(
        "patients.duplicate_primary_phone", "رقم الموبايل الأساسي مسجل لمريض آخر.");
    public static readonly ResultError ConcurrencyConflict = new(
        "patients.concurrency_conflict",
        "تم تعديل البيانات بواسطة مستخدم آخر. حدّث الصفحة ثم حاول مرة أخرى.");

    public static ResultError Validation(string message) =>
        new("patients.validation", message);
}
