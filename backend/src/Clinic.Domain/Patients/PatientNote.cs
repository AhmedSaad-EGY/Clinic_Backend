using Clinic.Domain.Common;

namespace Clinic.Domain.Patients;

public sealed class PatientNote : AggregateRoot
{
    private PatientNote() { }

    private PatientNote(long patientId, string noteText, PatientNoteVisibility visibility,
        long createdByUserId, DateTimeOffset createdAt)
    {
        PatientGuard.PositiveId(patientId, "المريض");
        PatientGuard.PositiveId(createdByUserId, "المستخدم المنشئ");
        if (!Enum.IsDefined(visibility))
        {
            throw new DomainException("درجة ظهور الملاحظة غير صحيحة.");
        }

        PatientId = patientId;
        NoteText = PatientGuard.RequiredText(noteText, "الملاحظة", 2000);
        Visibility = visibility;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public string NoteText { get; private set; } = string.Empty;
    public PatientNoteVisibility Visibility { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PatientNote Create(long patientId, string noteText,
        PatientNoteVisibility visibility, long createdByUserId, DateTimeOffset createdAt) =>
        new(patientId, noteText, visibility, createdByUserId, createdAt);

    public void Archive()
    {
        if (IsArchived)
        {
            throw new DomainException("الملاحظة مؤرشفة بالفعل.");
        }

        IsArchived = true;
    }
}
