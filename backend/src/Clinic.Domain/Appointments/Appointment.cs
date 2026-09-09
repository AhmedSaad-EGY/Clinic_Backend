using Clinic.Domain.Common;
using Clinic.Domain.Catalog;
using Clinic.Domain.Patients;

namespace Clinic.Domain.Appointments;

public sealed class Appointment : AggregateRoot
{
    private readonly List<AppointmentService> _services = [];

    private Appointment() { }

    private Appointment(long patientId, long roomId, long departmentId,
        DateTimeOffset startAt, AppointmentStatus status, long createdByUserId,
        DateTimeOffset createdAt)
    {
        AppointmentGuard.PositiveId(patientId, "المريض");
        AppointmentGuard.PositiveId(roomId, "الغرفة");
        AppointmentGuard.PositiveId(departmentId, "القسم");
        AppointmentGuard.PositiveId(createdByUserId, "المستخدم المنشئ");
        if (status is not AppointmentStatus.Booked and not AppointmentStatus.Suspended)
        {
            throw new DomainException("حالة إنشاء الحجز غير صحيحة.");
        }

        PatientId = patientId;
        RoomId = roomId;
        DepartmentId = departmentId;
        StartAt = EndAt = startAt;
        Status = status;
        StatusBeforeSuspension = status == AppointmentStatus.Suspended
            ? AppointmentStatus.Booked
            : null;
        PaymentStatus = PaymentStatus.Unpaid;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public long RoomId { get; private set; }
    public Room Room { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public AppointmentStatus? StatusBeforeSuspension { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? UpdatedByUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public long? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<AppointmentService> Services => _services;

    public bool ReservesResources => Status is AppointmentStatus.Booked or AppointmentStatus.Confirmed;

    public static Appointment Create(long patientId, long roomId, long departmentId,
        DateTimeOffset startAt, bool suspended, long createdByUserId,
        DateTimeOffset createdAt) => new(patientId, roomId, departmentId, startAt,
            suspended ? AppointmentStatus.Suspended : AppointmentStatus.Booked,
            createdByUserId, createdAt);

    public AppointmentService AddService(long serviceId, long doctorServiceId,
        int durationMinutes, int quantity, decimal unitPrice,
        IReadOnlyCollection<(long ServiceDeviceId, long DeviceId)> devices)
    {
        EnsureEditable();
        if (durationMinutes <= 0)
        {
            throw new DomainException("مدة الخدمة يجب أن تكون أكبر من صفر.");
        }

        DateTimeOffset segmentStart = _services
            .Where(item => item.Status == AppointmentServiceStatus.Scheduled)
            .Select(item => item.SegmentEndAt).DefaultIfEmpty(StartAt).Max();
        AppointmentService service = new(this, serviceId, doctorServiceId, DepartmentId,
            _services.Count + 1, segmentStart, segmentStart.AddMinutes(durationMinutes),
            quantity, unitPrice);
        foreach ((long serviceDeviceId, long deviceId) in devices)
        {
            service.AddDevice(serviceDeviceId, deviceId);
        }

        _services.Add(service);
        Recalculate();
        return service;
    }

    public void ReplaceSchedule(DateTimeOffset startAt, long updatedByUserId,
        DateTimeOffset updatedAt)
    {
        EnsureEditable();
        AppointmentGuard.PositiveId(updatedByUserId, "المستخدم المعدل");
        foreach (AppointmentService service in _services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            service.Supersede();
        }

        StartAt = EndAt = startAt;
        SubtotalAmount = DiscountAmount = NetAmount = 0;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = updatedAt;
    }

    public void Cancel(long actorUserId, DateTimeOffset cancelledAt, string? reason)
    {
        if (PaymentStatus != PaymentStatus.Unpaid ||
            Status is not AppointmentStatus.Booked and not AppointmentStatus.Suspended)
        {
            throw new DomainException("إلغاء الحجز المؤكد أو المدفوع يحتاج موافقة الأدمن.");
        }
        AppointmentGuard.PositiveId(actorUserId, "المستخدم الملغي");
        Status = AppointmentStatus.Cancelled;
        StatusBeforeSuspension = null;
        CancelledByUserId = actorUserId;
        CancelledAt = cancelledAt;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        foreach (AppointmentService service in _services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            service.Cancel();
        }
    }

    public void CancelAfterApproval(long adminUserId, DateTimeOffset cancelledAt,
        string reason)
    {
        bool confirmed = Status == AppointmentStatus.Confirmed ||
            Status == AppointmentStatus.Suspended &&
            StatusBeforeSuspension == AppointmentStatus.Confirmed;
        if (!confirmed)
        {
            throw new DomainException("هذا الحجز لا يحتاج أو لا يقبل موافقة إلغاء.");
        }

        AppointmentGuard.PositiveId(adminUserId, "الأدمن");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new DomainException("سبب الإلغاء مطلوب ويجب ألا يتجاوز 500 حرف.");
        }

        Status = AppointmentStatus.Cancelled;
        StatusBeforeSuspension = null;
        CancelledByUserId = adminUserId;
        CancelledAt = cancelledAt;
        CancellationReason = reason.Trim();
        UpdatedByUserId = adminUserId;
        UpdatedAt = cancelledAt;
        foreach (AppointmentService service in _services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            service.Cancel();
        }
    }

    public void RecordFullRefund(long actorUserId, DateTimeOffset refundedAt)
    {
        if (Status != AppointmentStatus.Cancelled || PaymentStatus != PaymentStatus.Paid)
        {
            throw new DomainException("لا يمكن استرداد قيمة هذا الحجز في حالته الحالية.");
        }

        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        PaymentStatus = PaymentStatus.Refunded;
        UpdatedByUserId = actorUserId;
        UpdatedAt = refundedAt;
    }

    public void Complete(long actorUserId, DateTimeOffset completedAt)
    {
        EnsureCanClose();
        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        Status = AppointmentStatus.Completed;
        UpdatedByUserId = actorUserId;
        UpdatedAt = completedAt;
        foreach (AppointmentService service in _services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            service.Complete();
        }
    }

    public void RecordFullPayment(long actorUserId, DateTimeOffset paidAt)
    {
        if (PaymentStatus != PaymentStatus.Unpaid ||
            Status is not AppointmentStatus.Booked and
                not AppointmentStatus.Confirmed and
                not AppointmentStatus.Completed)
        {
            throw new DomainException("لا يمكن تحصيل قيمة هذا الحجز في حالته الحالية.");
        }

        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        PaymentStatus = PaymentStatus.Paid;
        if (Status == AppointmentStatus.Booked)
        {
            Status = AppointmentStatus.Confirmed;
        }

        UpdatedByUserId = actorUserId;
        UpdatedAt = paidAt;
    }

    public void MarkNoShow(long actorUserId, DateTimeOffset changedAt)
    {
        EnsureCanClose();
        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        Status = AppointmentStatus.NoShow;
        UpdatedByUserId = actorUserId;
        UpdatedAt = changedAt;
        foreach (AppointmentService service in _services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            service.Cancel();
        }
    }

    public void Suspend(long actorUserId, DateTimeOffset changedAt)
    {
        if (Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            throw new DomainException("لا يمكن إيقاف حجز منتهٍ.");
        }

        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        if (Status != AppointmentStatus.Suspended)
        {
            StatusBeforeSuspension = Status;
        }
        Status = AppointmentStatus.Suspended;
        UpdatedByUserId = actorUserId;
        UpdatedAt = changedAt;
    }

    public void Reactivate(long actorUserId, DateTimeOffset changedAt)
    {
        if (Status != AppointmentStatus.Suspended)
        {
            throw new DomainException("الحجز غير موقوف.");
        }

        AppointmentGuard.PositiveId(actorUserId, "المستخدم");
        Status = RestoredStatus();
        StatusBeforeSuspension = null;
        UpdatedByUserId = actorUserId;
        UpdatedAt = changedAt;
    }

    public void ReactivateBySystem(DateTimeOffset changedAt)
    {
        if (Status != AppointmentStatus.Suspended)
        {
            throw new DomainException("الحجز غير موقوف.");
        }

        Status = RestoredStatus();
        StatusBeforeSuspension = null;
        UpdatedByUserId = null;
        UpdatedAt = changedAt;
    }

    private void Recalculate()
    {
        AppointmentService[] active = _services
            .Where(item => item.Status == AppointmentServiceStatus.Scheduled).ToArray();
        EndAt = active.Length == 0 ? StartAt : active.Max(item => item.SegmentEndAt);
        SubtotalAmount = active.Sum(item => item.GrossAmount);
        DiscountAmount = active.Sum(item => item.DiscountAmount);
        NetAmount = active.Sum(item => item.NetAmount);
    }

    public AppointmentService TransferDoctor(long appointmentServiceId,
        long doctorServiceId, long actorUserId, DateTimeOffset changedAt)
    {
        AppointmentGuard.PositiveId(actorUserId, "المستخدم المعدل");
        AppointmentService service = _services.SingleOrDefault(item =>
            item.Id == appointmentServiceId &&
            item.Status == AppointmentServiceStatus.Scheduled)
            ?? throw new DomainException("خدمة الحجز غير موجودة أو منتهية.");
        service.ChangeDoctorService(doctorServiceId);
        UpdatedByUserId = actorUserId;
        UpdatedAt = changedAt;
        return service;
    }

    private AppointmentStatus RestoredStatus() => StatusBeforeSuspension switch
    {
        AppointmentStatus.Booked => AppointmentStatus.Booked,
        AppointmentStatus.Confirmed => AppointmentStatus.Confirmed,
        _ => throw new DomainException("حالة الحجز السابقة للإيقاف غير صحيحة.")
    };

    private void EnsureEditable()
    {
        if (PaymentStatus != PaymentStatus.Unpaid ||
            Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            throw new DomainException("لا يمكن تعديل هذا الحجز.");
        }
    }

    private void EnsureCanClose()
    {
        if (Status is not AppointmentStatus.Booked and not AppointmentStatus.Confirmed)
        {
            throw new DomainException("لا يمكن إنهاء الحجز في حالته الحالية.");
        }
    }
}
