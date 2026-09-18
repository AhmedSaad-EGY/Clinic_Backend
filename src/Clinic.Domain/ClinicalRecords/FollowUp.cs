namespace Clinic.Domain.ClinicalRecords;

public sealed class FollowUp : AggregateRoot
{
    private FollowUp() { }

    private FollowUp(long prescriptionId, long patientId, long departmentId,
        DateOnly returnDate, long createdByUserId, DateTimeOffset createdAt)
    {
        if (prescriptionId <= 0 || patientId <= 0 || departmentId <= 0 ||
            createdByUserId <= 0)
            throw new DomainException("بيانات المتابعة غير صحيحة.");
        PrescriptionId = prescriptionId;
        PatientId = patientId;
        DepartmentId = departmentId;
        ReturnDate = returnDate;
        Status = FollowUpStatus.Due;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long PrescriptionId { get; private set; }
    public Prescription Prescription { get; private set; } = null!;
    public long PatientId { get; private set; }
    public long DepartmentId { get; private set; }
    public DateOnly ReturnDate { get; private set; }
    public FollowUpStatus Status { get; private set; }
    public long? ResultAppointmentId { get; private set; }
    public Appointment? ResultAppointment { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? ClosureReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FollowUp Create(long prescriptionId, long patientId,
        long departmentId, DateOnly returnDate, long actorUserId,
        DateTimeOffset createdAt) => new(prescriptionId, patientId, departmentId,
            returnDate, actorUserId, createdAt);

    public void UpdateDueDate(DateOnly returnDate) { EnsureDue(); ReturnDate = returnDate; }

    public void Complete(long appointmentId, long actorUserId,
        DateTimeOffset completedAt)
    {
        EnsureDue();
        if (appointmentId <= 0 || actorUserId <= 0)
            throw new DomainException("بيانات تحويل المتابعة إلى حجز غير صحيحة.");
        Status = FollowUpStatus.Completed;
        ResultAppointmentId = appointmentId;
        ClosedByUserId = actorUserId;
        ClosedAt = completedAt;
    }

    public void Cancel(long actorUserId, DateTimeOffset cancelledAt, string reason)
    {
        EnsureDue();
        if (actorUserId <= 0 || string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length > 500)
            throw new DomainException("سبب إغلاق المتابعة مطلوب.");
        Status = FollowUpStatus.Cancelled;
        ClosedByUserId = actorUserId;
        ClosedAt = cancelledAt;
        ClosureReason = reason.Trim();
    }

    private void EnsureDue()
    {
        if (Status != FollowUpStatus.Due)
            throw new DomainException("المتابعة مغلقة بالفعل.");
    }
}
