using Clinic.Domain.Common;
using Clinic.Domain.Patients;

namespace Clinic.Domain.Cashier;

public sealed class Payment : AggregateRoot
{
    private readonly List<PaymentMethodAllocation> _methodAllocations = [];
    private readonly List<AppointmentPaymentAllocation> _appointmentAllocations = [];

    private Payment()
    {
    }

    private Payment(string transactionNumber, Guid idempotencyKey,
        string requestFingerprint, long shiftId, long patientId,
        long collectedByUserId, DateTimeOffset collectedAt, string? note)
    {
        TransactionNumber = CashierGuard.RequiredText(transactionNumber,
            "رقم العملية", 40);
        if (idempotencyKey == Guid.Empty)
        {
            throw new DomainException("مفتاح منع التكرار غير صحيح.");
        }

        RequestFingerprint = CashierGuard.RequiredText(requestFingerprint,
            "بصمة الطلب", 64);
        if (RequestFingerprint.Length != 64)
        {
            throw new DomainException("بصمة طلب التحصيل غير صحيحة.");
        }

        CashierGuard.PositiveId(shiftId, "الشيفت");
        CashierGuard.PositiveId(patientId, "المريض");
        CashierGuard.PositiveId(collectedByUserId, "السكرتيرة");
        IdempotencyKey = idempotencyKey;
        ShiftId = shiftId;
        PatientId = patientId;
        CollectedByUserId = collectedByUserId;
        CollectedAt = collectedAt;
        Note = CashierGuard.OptionalText(note, 500);
        Status = PaymentRecordStatus.Posted;
    }

    public string TransactionNumber { get; private set; } = string.Empty;

    public Guid IdempotencyKey { get; private set; }

    public string RequestFingerprint { get; private set; } = string.Empty;

    public long ShiftId { get; private set; }

    public Shift Shift { get; private set; } = null!;

    public long PatientId { get; private set; }

    public Patient Patient { get; private set; } = null!;

    public decimal TotalAmount { get; private set; }

    public long CollectedByUserId { get; private set; }

    public DateTimeOffset CollectedAt { get; private set; }

    public PaymentRecordStatus Status { get; private set; }

    public string? Note { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<PaymentMethodAllocation> MethodAllocations =>
        _methodAllocations;

    public IReadOnlyCollection<AppointmentPaymentAllocation> AppointmentAllocations =>
        _appointmentAllocations;

    public static Payment Create(string transactionNumber, Guid idempotencyKey,
        string requestFingerprint, long shiftId, long patientId,
        long collectedByUserId, DateTimeOffset collectedAt, string? note,
        IReadOnlyCollection<(long PaymentMethodId, bool IsCash, decimal Amount,
            string? ReferenceNumber)> methods,
        IReadOnlyCollection<(long AppointmentId, decimal Amount)> appointments)
    {
        if (methods.Count == 0 || methods.Count > 4 ||
            methods.Select(item => item.PaymentMethodId).Distinct().Count() != methods.Count)
        {
            throw new DomainException("وسائل الدفع غير صحيحة أو مكررة.");
        }

        if (appointments.Count == 0 || appointments.Count > 20 ||
            appointments.Select(item => item.AppointmentId).Distinct().Count() != appointments.Count)
        {
            throw new DomainException("الحجوزات غير صحيحة أو مكررة.");
        }

        Payment payment = new(transactionNumber, idempotencyKey, requestFingerprint,
            shiftId, patientId, collectedByUserId, collectedAt, note);
        payment._methodAllocations.AddRange(methods.Select(item =>
            new PaymentMethodAllocation(payment, item.PaymentMethodId, item.IsCash,
                item.Amount, item.ReferenceNumber)));
        payment._appointmentAllocations.AddRange(appointments.Select(item =>
            new AppointmentPaymentAllocation(payment, item.AppointmentId,
                patientId, item.Amount)));

        decimal methodTotal = payment._methodAllocations.Sum(item => item.Amount);
        decimal appointmentTotal = payment._appointmentAllocations.Sum(item => item.Amount);
        if (methodTotal != appointmentTotal)
        {
            throw new DomainException("مجموع وسائل الدفع يجب أن يساوي قيمة الحجوزات.");
        }

        CashierGuard.PositiveMoney(appointmentTotal, "إجمالي التحصيل");
        payment.TotalAmount = appointmentTotal;
        return payment;
    }

    public void RecordRefund(decimal totalRefunded)
    {
        CashierGuard.PositiveMoney(totalRefunded, "إجمالي المبلغ المسترد");
        if (totalRefunded > TotalAmount)
        {
            throw new DomainException("إجمالي الاستردادات يتجاوز قيمة التحصيل.");
        }

        Status = totalRefunded == TotalAmount
            ? PaymentRecordStatus.Refunded
            : PaymentRecordStatus.PartiallyRefunded;
    }
}
