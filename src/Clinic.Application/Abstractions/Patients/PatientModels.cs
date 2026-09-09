using Clinic.Domain.Patients;

namespace Clinic.Application.Abstractions.Patients;

public sealed record PatientInput(
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

public sealed record PatientSummary(
    long Id,
    string FileNumber,
    string FullName,
    string PrimaryPhoneNumber,
    string? SecondaryPhoneNumber,
    int CurrentAge,
    PatientGender Gender,
    string? Area,
    bool IsArchived,
    string RowVersion);

public sealed record PatientDetails(
    long Id,
    string FileNumber,
    string FullName,
    string PrimaryPhoneNumber,
    string? SecondaryPhoneNumber,
    DateOnly? BirthDate,
    int? RecordedAge,
    DateOnly? AgeRecordedAt,
    int CurrentAge,
    PatientGender Gender,
    string? Area,
    string? Address,
    string? Email,
    string? GuardianName,
    string? GuardianPhoneNumber,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string RowVersion);

public sealed record PatientNoteModel(
    long Id,
    long PatientId,
    string NoteText,
    PatientNoteVisibility Visibility,
    long CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool IsArchived,
    string RowVersion);

public sealed record SensitiveNoteReceipt(long Id, DateTimeOffset CreatedAt);

public sealed record TreatmentHistoryModel(
    long Id,
    long PatientId,
    DateOnly EventDate,
    string Description,
    long CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool IsArchived,
    string RowVersion);

public sealed record PatientPage<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
