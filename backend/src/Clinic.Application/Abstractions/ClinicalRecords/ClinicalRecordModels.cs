using Clinic.Application.Common;
using Clinic.Domain.ClinicalRecords;

namespace Clinic.Application.Abstractions.ClinicalRecords;

public sealed record PrescriptionItemInput(string MedicineName,
    decimal? DoseAmount, string? DoseUnit, int? TimesPerDay,
    string? FrequencyText, string? DurationText, FoodTiming? FoodTiming,
    string? Instructions);

public sealed record PrescriptionContentInput(
    IReadOnlyCollection<PrescriptionItemInput> Items,
    DateOnly? ReturnDate, int? ReturnAfterDays);

public sealed record PrescriptionItemModel(long Id, int SortOrder,
    string MedicineName, decimal? DoseAmount, string? DoseUnit,
    int? TimesPerDay, string? FrequencyText, string? DurationText,
    FoodTiming? FoodTiming, string? Instructions);

public sealed record PrescriptionRevisionModel(long Id, int RevisionNumber,
    long CreatedByUserId, string CreatedByName, DateTimeOffset CreatedAt,
    string? ChangeReason, DateOnly? ReturnDate,
    IReadOnlyCollection<PrescriptionItemModel> Items);

public sealed record PrescriptionModel(long Id, long AppointmentServiceId,
    long AppointmentId, long PatientId, string PatientFileNumber,
    string PatientName, long DoctorId, string DoctorName, long DepartmentId,
    string DepartmentName, PrescriptionStatus Status, long CreatedByUserId,
    string CreatedByName, DateTimeOffset CreatedAt, DateTimeOffset? FinalizedAt,
    DateTimeOffset? VoidedAt, string? VoidReason,
    PrescriptionRevisionModel CurrentRevision, string RowVersion);

public sealed record PrescriptionSummary(long Id, long AppointmentServiceId,
    long AppointmentId, long PatientId, string PatientName, long DoctorId,
    string DoctorName, string DepartmentName, PrescriptionStatus Status,
    DateTimeOffset CreatedAt, DateOnly? ReturnDate, int RevisionNumber,
    string RowVersion);

public sealed record FollowUpModel(long Id, long PrescriptionId, long PatientId,
    string PatientFileNumber, string PatientName, long DepartmentId,
    string DepartmentName, long DoctorId, string DoctorName,
    DateOnly ReturnDate, DateOnly? CurrentPrescriptionReturnDate,
    FollowUpStatus Status, long? ResultAppointmentId,
    Clinic.Domain.Appointments.AppointmentStatus? ResultAppointmentStatus,
    DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt,
    string? ClosureReason, string RowVersion);

public sealed record ClinicalPage<T>(IReadOnlyCollection<T> Items,
    int PageNumber, int PageSize, int TotalCount);

public sealed record FollowUpSearch(long? PatientId, long? DepartmentId,
    long? DoctorId, FollowUpStatus? Status, DateOnly? FromDate,
    DateOnly? ToDate, bool OverdueOnly, bool IncludeArchivedPatient,
    int PageNumber, int PageSize);

public interface IPrescriptionCommandService
{
    Task<Result<PrescriptionModel>> CreateDraftAsync(long actorUserId,
        long appointmentServiceId, PrescriptionContentInput content,
        CancellationToken cancellationToken);
    Task<Result<PrescriptionModel>> SaveDraftAsync(long actorUserId,
        long prescriptionId, PrescriptionContentInput content, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<PrescriptionModel>> FinalizeAsync(long actorUserId,
        long prescriptionId, bool matchesDoctorPrescription, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<PrescriptionModel>> CorrectAsync(long actorUserId,
        long prescriptionId, PrescriptionContentInput content, string reason,
        bool matchesDoctorPrescription, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<PrescriptionModel>> VoidAsync(long actorUserId,
        long prescriptionId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
}

public interface IClinicalRecordQueryService
{
    Task<Result<PrescriptionModel>> GetAsync(long prescriptionId,
        bool includeArchivedPatient,
        CancellationToken cancellationToken);
    Task<Result<ClinicalPage<PrescriptionSummary>>> ListPatientAsync(long patientId,
        bool includeArchivedPatient, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<PrescriptionRevisionModel>>> ListRevisionsAsync(
        long prescriptionId, CancellationToken cancellationToken);
    Task<Result<PrescriptionRevisionModel>> GetRevisionAsync(long prescriptionId,
        long revisionId, CancellationToken cancellationToken);
    Task<Result<FollowUpModel>> GetFollowUpAsync(long followUpId,
        bool includeArchivedPatient,
        CancellationToken cancellationToken);
    Task<Result<ClinicalPage<FollowUpModel>>> SearchFollowUpsAsync(
        FollowUpSearch search, CancellationToken cancellationToken);
}

public interface IFollowUpCommandService
{
    Task<Result<FollowUpModel>> CancelFollowUpAsync(long actorUserId,
        long followUpId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
}
