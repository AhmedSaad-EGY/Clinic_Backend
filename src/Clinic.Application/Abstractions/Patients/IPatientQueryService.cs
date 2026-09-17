namespace Clinic.Application.Abstractions.Patients;

public interface IPatientQueryService
{
    Task<Result<PatientPage<PatientSummary>>> ListPatientsAsync(string? search,
        string? normalizedPhone, long? fileNumber, PatientGender? gender, string? area,
        bool includeArchived, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<PatientDetails>> GetPatientAsync(long patientId, bool includeArchived,
        CancellationToken cancellationToken);
    Task<Result<PatientPage<PatientNoteModel>>> ListStaffNotesAsync(long patientId,
        bool includeArchived, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<PatientPage<PatientNoteModel>>> ListAllNotesAsync(long patientId,
        bool includeArchived, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<PatientPage<TreatmentHistoryModel>>> ListTreatmentHistoryAsync(long patientId,
        bool includeArchived, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
