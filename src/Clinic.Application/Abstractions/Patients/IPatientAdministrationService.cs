namespace Clinic.Application.Abstractions.Patients;

public interface IPatientAdministrationService
{
    Task<Result<PatientDetails>> CreatePatientAsync(long actorUserId, PatientInput input,
        CancellationToken cancellationToken);
    Task<Result<PatientDetails>> UpdatePatientAsync(long actorUserId, long patientId,
        PatientInput input, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<Result> ArchivePatientAsync(long actorUserId, long patientId, string reason,
        byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<Result<PatientDetails>> RestorePatientAsync(long actorUserId, long patientId,
        string reason, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<Result<SensitiveNoteReceipt>> CreateNoteAsync(long actorUserId, long patientId,
        string noteText, PatientNoteVisibility visibility, CancellationToken cancellationToken);
    Task<Result> ArchiveNoteAsync(long actorUserId, long noteId, string reason,
        byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<Result<TreatmentHistoryModel>> CreateTreatmentHistoryAsync(long actorUserId,
        long patientId, DateOnly eventDate, string description,
        CancellationToken cancellationToken);
    Task<Result> ArchiveTreatmentHistoryAsync(long actorUserId, long treatmentHistoryId,
        string reason, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
