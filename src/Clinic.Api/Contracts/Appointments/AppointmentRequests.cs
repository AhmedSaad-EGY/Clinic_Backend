namespace Clinic.Api.Contracts.Appointments;

public sealed record AppointmentLineRequest(long ServiceId, long DoctorId, int Quantity,
    IReadOnlyCollection<long>? OptionalDeviceIds);

public sealed record FollowUpBookingRequest(long FollowUpId, string RowVersion);

public sealed record CreateAppointmentRequest(long PatientId, long DepartmentId,
    DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
    FollowUpBookingRequest? FollowUp = null, long? PatientPackageId = null);

public sealed record DiscountOverrideRequest(DiscountOverrideMode Mode, long? DiscountId,
    string? Reason);

public sealed record AdminCreateAppointmentRequest(long PatientId, long DepartmentId,
    DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
    FollowUpBookingRequest? FollowUp = null, long? PatientPackageId = null,
    DiscountOverrideRequest? DiscountOverride = null);

public sealed record UpdateAppointmentRequest(long PatientId, long DepartmentId,
    DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
    string RowVersion, long? PatientPackageId = null);

public sealed record CheckAppointmentAvailabilityRequest(long PatientId, long DepartmentId,
    DateTimeOffset StartAt, IReadOnlyCollection<AppointmentLineRequest> Services,
    long? ExcludedAppointmentId, long? PatientPackageId = null);

public sealed record ChangeAppointmentStateRequest(string RowVersion, string? Reason = null);
public sealed record TransferAppointmentDoctorRequest(long DoctorId, string Reason,
    string RowVersion);

public sealed record AppointmentSearchRequest(DateTimeOffset? From, DateTimeOffset? To,
    long? DepartmentId, long? SpecializationId, long? ServiceId, long? DoctorId,
    AppointmentStatus? Status, PaymentStatus? PaymentStatus, long? CreatedByUserId,
    string? PatientSearch, int? MinimumAge, int? MaximumAge, PatientGender? Gender,
    string? Area, int PageNumber = 1, int PageSize = 20);
