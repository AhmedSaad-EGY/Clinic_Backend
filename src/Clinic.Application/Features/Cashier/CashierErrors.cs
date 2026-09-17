namespace Clinic.Application.Features.Cashier;

public static class CashierErrors
{
    public static readonly ResultError NotAuthenticated = new(
        "cashier.not_authenticated", "يجب تسجيل الدخول أولًا.");
    public static readonly ResultError Forbidden = new(
        "cashier.forbidden", "لا تملك صلاحية تنفيذ هذا الإجراء.");
    public static readonly ResultError DrawerNotFound = new(
        "cashier.drawer_not_found", "درج الكاش غير موجود أو غير فعال.");
    public static readonly ResultError ShiftNotFound = new(
        "cashier.shift_not_found", "الشيفت غير موجود.");
    public static readonly ResultError PolicyNotFound = new(
        "cashier.policy_not_found", "سياسة الشيفت غير موجودة.");
    public static readonly ResultError PaymentNotFound = new(
        "cashier.payment_not_found", "عملية التحصيل غير موجودة.");
    public static readonly ResultError PaymentMethodNotFound = new(
        "cashier.payment_method_not_found", "وسيلة دفع غير موجودة أو غير فعالة.");
    public static readonly ResultError ApprovalRequestNotFound = new(
        "cashier.approval_request_not_found", "طلب الموافقة غير موجود.");
    public static readonly ResultError RefundNotFound = new(
        "cashier.refund_not_found", "عملية الاسترداد غير موجودة.");
    public static readonly ResultError CashWithdrawalNotFound = new(
        "cashier.cash_withdrawal_not_found", "طلب السحب النقدي غير موجود.");
    public static readonly ResultError AppointmentNotPayable = new(
        "cashier.appointment_not_payable",
        "أحد الحجوزات غير قابل للتحصيل أو تم دفعه بالفعل.");
    public static readonly ResultError PatientPackageNotPayable = new(
        "cashier.patient_package_not_payable",
        "إحدى الباقات غير قابلة للتحصيل أو تم دفعها بالفعل.");
    public static readonly ResultError IdempotencyConflict = new(
        "cashier.idempotency_conflict",
        "استُخدم مفتاح منع التكرار سابقًا مع بيانات مختلفة.");
    public static readonly ResultError ConcurrencyConflict = new(
        "cashier.concurrency_conflict",
        "تم تعديل البيانات بواسطة مستخدم آخر. حدّث الصفحة وحاول مرة أخرى.");
    public static readonly ResultError ReconciliationStale = new(
        "cashier.reconciliation_stale",
        "تغير المبلغ المتوقع. أجرِ المطابقة مرة أخرى قبل الإغلاق.");
    public static readonly ResultError ApprovalRequired = new(
        "cashier.approval_required", "إلغاء هذا الحجز يحتاج موافقة الأدمن.");

    public static ResultError Validation(string message) =>
        new("cashier.validation", message);

    public static ResultError Conflict(string message,
        IReadOnlyCollection<ShiftConflictModel>? conflicts = null) => new(
            "cashier.conflict",
            message,
            conflicts is null
                ? null
                : new Dictionary<string, object?> { ["conflicts"] = conflicts });
}
