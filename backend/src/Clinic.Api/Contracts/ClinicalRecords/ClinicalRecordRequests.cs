using Clinic.Domain.ClinicalRecords;

namespace Clinic.Api.Contracts.ClinicalRecords;

public sealed record PrescriptionItemRequest(string MedicineName,
    decimal? DoseAmount, string? DoseUnit, int? TimesPerDay,
    string? FrequencyText, string? DurationText, FoodTiming? FoodTiming,
    string? Instructions);

public sealed record PrescriptionContentRequest(
    IReadOnlyCollection<PrescriptionItemRequest> Items,
    DateOnly? ReturnDate, int? ReturnAfterDays);

public sealed record CreatePrescriptionRequest(long AppointmentServiceId,
    PrescriptionContentRequest Content);

public sealed record SavePrescriptionDraftRequest(PrescriptionContentRequest Content,
    string RowVersion);

public sealed record FinalizePrescriptionRequest(bool MatchesDoctorPrescription,
    string RowVersion);

public sealed record CorrectPrescriptionRequest(PrescriptionContentRequest Content,
    string Reason, bool MatchesDoctorPrescription, string RowVersion);

public sealed record ClinicalReasonRequest(string Reason, string RowVersion);
