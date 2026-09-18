namespace Clinic.Domain.ClinicalRecords;

public sealed class Prescription : AggregateRoot
{
    private readonly List<PrescriptionRevision> _revisions = [];
    private Prescription() { }

    private Prescription(long appointmentServiceId, long createdByUserId,
        DateTimeOffset createdAt)
    {
        if (appointmentServiceId <= 0 || createdByUserId <= 0)
            throw new DomainException("بيانات إنشاء الروشتة غير صحيحة.");
        AppointmentServiceId = appointmentServiceId;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Status = PrescriptionStatus.Draft;
    }

    public long AppointmentServiceId { get; private set; }
    public AppointmentService AppointmentService { get; private set; } = null!;
    public long? CurrentRevisionId { get; private set; }
    public PrescriptionRevision? CurrentRevision { get; private set; }
    public PrescriptionStatus Status { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public long? FinalizedByUserId { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public long? VoidedByAdminUserId { get; private set; }
    public string? VoidReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PrescriptionRevision> Revisions => _revisions;

    public static Prescription Create(long appointmentServiceId, long actorUserId,
        DateTimeOffset createdAt, PrescriptionContent content)
    {
        Prescription prescription = new(appointmentServiceId, actorUserId, createdAt);
        prescription.AddRevision(actorUserId, createdAt, null, content);
        return prescription;
    }

    public PrescriptionRevision SaveDraft(long actorUserId, DateTimeOffset changedAt,
        PrescriptionContent content)
    {
        EnsureStatus(PrescriptionStatus.Draft, "لا يمكن تعديل روشتة تم اعتمادها.");
        return AddRevision(actorUserId, changedAt, null, content);
    }

    public PrescriptionRevision Correct(long actorUserId, DateTimeOffset changedAt,
        string reason, PrescriptionContent content)
    {
        EnsureStatus(PrescriptionStatus.Finalized,
            "لا يمكن تصحيح الروشتة في حالتها الحالية.");
        if (content.Items.Count == 0)
            throw new DomainException("يجب أن تحتوي الروشتة النهائية على دواء واحد على الأقل.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new DomainException("سبب تصحيح الروشتة مطلوب.");
        return AddRevision(actorUserId, changedAt, reason.Trim(), content);
    }

    public void SelectCurrentRevision(PrescriptionRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!_revisions.Contains(revision) || revision.Id <= 0)
            throw new DomainException("نسخة الروشتة الحالية غير صحيحة.");
        CurrentRevision = revision;
        CurrentRevisionId = revision.Id;
    }

    public void Finalize(long actorUserId, DateTimeOffset finalizedAt,
        bool matchesDoctorPrescription)
    {
        EnsureStatus(PrescriptionStatus.Draft,
            "الروشتة معتمدة بالفعل أو مبطلة.");
        if (!matchesDoctorPrescription || CurrentRevision is null ||
            CurrentRevision.Items.Count == 0 || actorUserId <= 0)
            throw new DomainException(
                "يجب تأكيد مطابقة الروشتة وإضافة دواء واحد على الأقل.");
        Status = PrescriptionStatus.Finalized;
        FinalizedByUserId = actorUserId;
        FinalizedAt = finalizedAt;
    }

    public void Void(long adminUserId, DateTimeOffset voidedAt, string reason)
    {
        if (Status == PrescriptionStatus.Voided || adminUserId <= 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new DomainException("سبب إبطال الروشتة مطلوب وحالتها يجب أن تسمح بذلك.");
        Status = PrescriptionStatus.Voided;
        VoidedByAdminUserId = adminUserId;
        VoidedAt = voidedAt;
        VoidReason = reason.Trim();
    }

    private PrescriptionRevision AddRevision(long actorUserId,
        DateTimeOffset createdAt, string? changeReason, PrescriptionContent content)
    {
        PrescriptionRevision revision = new(this, _revisions.Count + 1,
            actorUserId, createdAt, changeReason, content.ReturnDate, content.Items);
        _revisions.Add(revision);
        return revision;
    }

    private void EnsureStatus(PrescriptionStatus expected, string message)
    {
        if (Status != expected) throw new DomainException(message);
    }
}

public sealed record PrescriptionContent(DateOnly? ReturnDate,
    IReadOnlyCollection<(string MedicineName, decimal? DoseAmount,
        string? DoseUnit, int? TimesPerDay, string? FrequencyText,
        string? DurationText, FoodTiming? FoodTiming, string? Instructions)> Items);
