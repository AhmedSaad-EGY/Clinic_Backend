using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Identity;

public static class IdentityErrors
{
    public static readonly ResultError InvalidCredentials = new(
        "identity.invalid_credentials",
        "اسم المستخدم أو كلمة المرور غير صحيحة.");

    public static readonly ResultError AccountLocked = new(
        "identity.account_locked",
        "تم قفل الحساب مؤقتًا بعد محاولات دخول غير ناجحة.");

    public static readonly ResultError AccountDisabled = new(
        "identity.account_disabled",
        "الحساب غير متاح. تواصل مع مسؤول النظام.");

    public static readonly ResultError NotAuthenticated = new(
        "identity.not_authenticated",
        "يجب تسجيل الدخول أولًا.");

    public static readonly ResultError Forbidden = new(
        "identity.forbidden",
        "لا تملك صلاحية تنفيذ هذه العملية.");

    public static readonly ResultError UserNotFound = new(
        "identity.user_not_found",
        "الحساب المطلوب غير موجود.");

    public static readonly ResultError DuplicateUserName = new(
        "identity.duplicate_username",
        "اسم المستخدم مستخدم بالفعل.");

    public static readonly ResultError DuplicatePhone = new(
        "identity.duplicate_phone",
        "رقم الهاتف مستخدم بالفعل.");

    public static readonly ResultError DuplicateEmail = new(
        "identity.duplicate_email",
        "البريد الإلكتروني مستخدم بالفعل.");

    public static readonly ResultError CurrentPasswordIncorrect = new(
        "identity.current_password_incorrect",
        "كلمة المرور الحالية غير صحيحة.");

    public static ResultError Validation(string description) =>
        new("identity.validation", description);

    public static ResultError OperationFailed(string description) =>
        new("identity.operation_failed", description);
}
