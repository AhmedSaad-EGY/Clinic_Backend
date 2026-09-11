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
    public static readonly ResultError DuplicatePhoneConflict = new(
        "patients.duplicate_phone", "رقم الموبايل مسجل في ملف مريض آخر.");
    public static readonly ResultError ConcurrencyConflict = new(
        "patients.concurrency_conflict",
        "تم تعديل البيانات بواسطة مستخدم آخر. حدّث الصفحة ثم حاول مرة أخرى.");

    public static ResultError Validation(string message) =>
        new("patients.validation", message);

    public static ResultError DuplicatePhone(ExistingPatientReference patient) => new(
        "patients.duplicate_phone",
        "هذا المريض مسجل بالفعل. افتح الملف الموجود بدلًا من إنشاء ملف جديد.",
        new Dictionary<string, object?> { ["existingPatient"] = patient });
}
