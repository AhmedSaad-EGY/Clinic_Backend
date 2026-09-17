namespace Clinic.Api.Contracts.Patients;

public sealed record CreatePatientRequest(
    string FullName,
    string PrimaryPhoneNumber,
    string? SecondaryPhoneNumber,
    DateOnly? BirthDate,
    int? Age,
    PatientGender Gender,
    string? Area,
    string? Address,
    string? Email,
    string? GuardianName,
    string? GuardianPhoneNumber);

public sealed record UpdatePatientRequest(
    string FullName,
    string PrimaryPhoneNumber,
    string? SecondaryPhoneNumber,
    DateOnly? BirthDate,
    int? Age,
    PatientGender Gender,
    string? Area,
    string? Address,
    string? Email,
    string? GuardianName,
    string? GuardianPhoneNumber,
    string RowVersion);

public sealed record CreatePatientNoteRequest(string NoteText, PatientNoteVisibility Visibility);

public sealed record CreateTreatmentHistoryRequest(DateOnly EventDate, string Description);

public sealed record ArchivePatientRecordRequest(string Reason, string RowVersion);
