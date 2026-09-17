namespace Clinic.Application.Abstractions.Catalog;

public static class CatalogErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "catalog.not_authenticated",
        "يجب تسجيل الدخول أولًا.");

    public static readonly ResultError DepartmentNotFound = new(
        "catalog.department_not_found",
        "القسم غير موجود.");

    public static readonly ResultError SpecializationNotFound = new(
        "catalog.specialization_not_found",
        "التخصص غير موجود.");

    public static readonly ResultError ServiceNotFound = new(
        "catalog.service_not_found",
        "الخدمة غير موجودة.");

    public static readonly ResultError DeviceNotFound = new(
        "catalog.device_not_found",
        "الجهاز غير موجود.");

    public static readonly ResultError DuplicateName = new(
        "catalog.duplicate_name",
        "يوجد سجل آخر بنفس الاسم في النطاق المحدد.");

    public static readonly ResultError DuplicateIdentifier = new(
        "catalog.duplicate_identifier",
        "رقم الجهاز مستخدم بالفعل.");

    public static readonly ResultError ConcurrencyConflict = new(
        "catalog.concurrency_conflict",
        "تم تعديل البيانات بواسطة مستخدم آخر. حدّث الصفحة ثم حاول مرة أخرى.");

    public static ResultError Validation(string message) =>
        new("catalog.validation", message);

    public static ResultError DependencyConflict(string message) =>
        new("catalog.dependency_conflict", message);
}
