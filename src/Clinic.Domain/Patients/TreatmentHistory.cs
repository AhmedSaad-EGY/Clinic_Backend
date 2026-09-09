using Clinic.Domain.Common;

namespace Clinic.Domain.Patients;

public sealed class TreatmentHistory : AggregateRoot
{
    private TreatmentHistory() { }

    private TreatmentHistory(long patientId, DateOnly eventDate, string description,
        long createdByUserId, DateOnly recordedOn, DateTimeOffset createdAt)
    {
        PatientGuard.PositiveId(patientId, "المريض");
        PatientGuard.PositiveId(createdByUserId, "المستخدم المنشئ");
        if (eventDate == default || eventDate > recordedOn ||
            eventDate < recordedOn.AddYears(-130))
        {
            throw new DomainException("تاريخ الحدث العلاجي غير صحيح.");
        }

        PatientId = patientId;
        EventDate = eventDate;
        Description = PatientGuard.RequiredText(description, "وصف التاريخ العلاجي", 2000);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public DateOnly EventDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static TreatmentHistory Create(long patientId, DateOnly eventDate,
        string description, long createdByUserId, DateOnly recordedOn,
        DateTimeOffset createdAt) =>
        new(patientId, eventDate, description, createdByUserId, recordedOn, createdAt);

    public void Archive()
    {
        if (IsArchived)
        {
            throw new DomainException("سجل التاريخ العلاجي مؤرشف بالفعل.");
        }

        IsArchived = true;
    }
}
