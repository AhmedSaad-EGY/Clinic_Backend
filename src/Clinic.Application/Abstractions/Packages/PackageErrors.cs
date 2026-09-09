using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Packages;

public static class PackageErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "packages.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError NotFound = new(
        "packages.not_found", "الباقة غير موجودة.");
    public static readonly ResultError DepartmentNotFound = new(
        "packages.department_not_found", "القسم غير موجود.");
    public static readonly ResultError ServiceNotFound = new(
        "packages.service_not_found", "إحدى خدمات الباقة غير موجودة أو غير متاحة.");
    public static readonly ResultError DuplicateName = new(
        "packages.duplicate_name", "يوجد بالفعل باقة بنفس الاسم.");
    public static readonly ResultError ConcurrencyConflict = new(
        "packages.concurrency_conflict",
        "تم تعديل الباقة بواسطة مستخدم آخر. حدّث الصفحة ثم حاول مرة أخرى.");
    public static readonly ResultError PatientNotFound = new(
        "patient_packages.patient_not_found", "المريض غير موجود أو مؤرشف.");
    public static readonly ResultError DefinitionUnavailable = new(
        "patient_packages.definition_unavailable", "الباقة غير متاحة للتسجيل حاليًا.");
    public static readonly ResultError DefinitionIncomplete = new(
        "patient_packages.definition_incomplete",
        "يجب استكمال مهلة بدء الاستخدام ومدة الاستخدام في تعريف الباقة.");
    public static readonly ResultError PatientPackageNotFound = new(
        "patient_packages.not_found", "باقة المريض غير موجودة.");
    public static readonly ResultError IdempotencyConflict = new(
        "patient_packages.idempotency_conflict",
        "تم استخدام مفتاح منع التكرار مسبقًا مع طلب مختلف.");

    public static ResultError Validation(string message) => new("packages.validation", message);
}
