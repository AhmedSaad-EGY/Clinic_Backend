namespace Clinic.Application.Abstractions.Patients;

public enum PatientTimelineRecordType
{
    Appointment = 1,
    Payment = 2,
    Refund = 3,
    PatientPackage = 4,
    PackageSession = 5,
    Prescription = 6,
    FollowUp = 7,
    TreatmentHistory = 8,
    Note = 9
}

public sealed record PatientTimelineFilter(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyCollection<PatientTimelineRecordType> RecordTypes,
    bool IncludeArchivedRecords,
    int PageNumber,
    int PageSize);

public sealed record PatientTimelineItem(
    long RecordId,
    long? ParentRecordId,
    PatientTimelineRecordType RecordType,
    DateOnly OccurredOn,
    DateTimeOffset OccurredAt,
    string Title,
    string Summary,
    string? Status,
    decimal? Amount,
    bool IsArchived);

public sealed record PatientTimelinePage(
    PatientDetails Patient,
    IReadOnlyCollection<PatientTimelineItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
