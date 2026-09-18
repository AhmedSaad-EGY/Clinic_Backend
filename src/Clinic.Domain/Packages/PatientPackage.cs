namespace Clinic.Domain.Packages;

public sealed class PatientPackage : AggregateRoot
{
    private readonly List<PatientPackageService> _services = [];

    private PatientPackage()
    {
    }

    private PatientPackage(Patient patient, Package package, Guid idempotencyKey,
        string requestFingerprint, long actorUserId, DateTimeOffset registeredAt,
        Discount? discount)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(package);
        if (patient.IsArchived)
        {
            throw new DomainException("لا يمكن تسجيل باقة لمريض مؤرشف.");
        }

        if (package.ActivationGraceDays is not int activationGraceDays ||
            package.UsageDurationDays is not int usageDurationDays)
        {
            throw new DomainException("يجب استكمال مهلة البداية ومدة الاستخدام قبل تسجيل الباقة.");
        }

        ValidateIdentifier(actorUserId, "المستخدم");
        if (idempotencyKey == Guid.Empty || requestFingerprint is null ||
            requestFingerprint.Length != 64)
        {
            throw new DomainException("بيانات منع تكرار الطلب غير صحيحة.");
        }

        Patient = patient;
        PatientId = patient.Id;
        Package = package;
        PackageId = package.Id;
        DepartmentId = package.DepartmentId;
        PackageNameSnapshot = package.Name;
        DepartmentNameSnapshot = package.Department.Name;
        TotalSessions = package.SessionCount;
        BasePriceSnapshot = package.BasePrice;
        Discount = discount;
        DiscountId = discount?.Id;
        DiscountAmountSnapshot = discount?.Calculate(package.BasePrice) ?? 0;
        NetPriceSnapshot = package.BasePrice - DiscountAmountSnapshot;
        ActivationGraceDaysSnapshot = activationGraceDays;
        UsageDurationDaysSnapshot = usageDurationDays;
        PaymentStatus = NetPriceSnapshot == 0
            ? PatientPackagePaymentStatus.NotRequired
            : PatientPackagePaymentStatus.Unpaid;
        Status = PatientPackageStatus.Active;
        RegisteredAt = registeredAt;
        RegisteredByUserId = actorUserId;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;

        if (PaymentStatus == PatientPackagePaymentStatus.NotRequired)
        {
            ActivationWindowStartedAt = registeredAt;
            ActivationDeadlineAt = registeredAt.AddDays(activationGraceDays);
        }

        foreach (PackageService line in package.Services.Where(item => item.IsActive))
        {
            _services.Add(new PatientPackageService(this, line));
        }

        if (_services.Count is < 1 or > Package.MaximumServiceCount ||
            _services.Sum(item => item.SessionsPurchased) != TotalSessions)
        {
            throw new DomainException("مكونات الباقة لا تطابق إجمالي الجلسات.");
        }
    }

    public long PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public long PackageId { get; private set; }
    public Package Package { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public string PackageNameSnapshot { get; private set; } = string.Empty;
    public string DepartmentNameSnapshot { get; private set; } = string.Empty;
    public int TotalSessions { get; private set; }
    public decimal BasePriceSnapshot { get; private set; }
    public long? DiscountId { get; private set; }
    public Discount? Discount { get; private set; }
    public decimal DiscountAmountSnapshot { get; private set; }
    public decimal NetPriceSnapshot { get; private set; }
    public int ActivationGraceDaysSnapshot { get; private set; }
    public int UsageDurationDaysSnapshot { get; private set; }
    public PatientPackagePaymentStatus PaymentStatus { get; private set; }
    public PatientPackageStatus Status { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public long RegisteredByUserId { get; private set; }
    public DateTimeOffset? ActivationWindowStartedAt { get; private set; }
    public DateTimeOffset? ActivationDeadlineAt { get; private set; }
    public DateTimeOffset? FirstUsedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public long? UpdatedByAdminUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PatientPackageService> Services => _services;

    public static PatientPackage Register(Patient patient, Package package, Guid idempotencyKey,
        string requestFingerprint, long actorUserId, DateTimeOffset registeredAt,
        Discount? discount = null) => new(patient, package, idempotencyKey,
            requestFingerprint, actorUserId, registeredAt, discount);

    public void Extend(PatientPackageExtensionType extensionType, DateTimeOffset newDeadline,
        long adminUserId, DateTimeOffset updatedAt)
    {
        ValidateIdentifier(adminUserId, "الأدمن");
        DateTimeOffset current = extensionType switch
        {
            PatientPackageExtensionType.ActivationDeadline when FirstUsedAt is null &&
                ActivationDeadlineAt.HasValue => ActivationDeadlineAt.Value,
            PatientPackageExtensionType.UsageExpiry when ExpiresAt.HasValue => ExpiresAt.Value,
            _ => throw new DomainException("لا يوجد موعد صالح يمكن تمديده لهذه الباقة.")
        };

        if (newDeadline <= current)
        {
            throw new DomainException("يجب أن يكون الموعد الجديد بعد الموعد الحالي.");
        }

        if (extensionType == PatientPackageExtensionType.ActivationDeadline)
        {
            ActivationDeadlineAt = newDeadline;
        }
        else
        {
            ExpiresAt = newDeadline;
        }

        UpdatedByAdminUserId = adminUserId;
        UpdatedAt = updatedAt;
    }

    public void RecordFullPayment(decimal amount, DateTimeOffset collectedAt)
    {
        if (PaymentStatus != PatientPackagePaymentStatus.Unpaid ||
            amount != NetPriceSnapshot)
        {
            throw new DomainException("الباقة غير قابلة للتحصيل أو المبلغ لا يساوي قيمتها الكاملة.");
        }

        PaymentStatus = PatientPackagePaymentStatus.Paid;
        ActivationWindowStartedAt = collectedAt;
        ActivationDeadlineAt = collectedAt.AddDays(ActivationGraceDaysSnapshot);
    }

    public void RecordFirstUse(DateTimeOffset visitStartedAt)
    {
        if (FirstUsedAt.HasValue)
        {
            return;
        }

        if (PaymentStatus == PatientPackagePaymentStatus.Unpaid ||
            ActivationDeadlineAt is null || visitStartedAt >= ActivationDeadlineAt.Value)
        {
            throw new DomainException("موعد الجلسة خارج مهلة بدء استخدام الباقة.");
        }

        FirstUsedAt = visitStartedAt;
        ExpiresAt = visitStartedAt.AddDays(UsageDurationDaysSnapshot);
    }

    private static void ValidateIdentifier(long value, string fieldName)
    {
        if (value <= 0)
        {
            throw new DomainException($"{fieldName} غير صحيح.");
        }
    }
}
