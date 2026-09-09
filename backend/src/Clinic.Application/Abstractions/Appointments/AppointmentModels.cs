using Clinic.Domain.Appointments;
using Clinic.Domain.Patients;

namespace Clinic.Application.Abstractions.Appointments;

public sealed record AppointmentLineInput(
    long ServiceId,
    long DoctorId,
    int Quantity,
    IReadOnlyCollection<long> OptionalDeviceIds);

public sealed record FollowUpBookingInput(long FollowUpId, string RowVersion);

public sealed record AppointmentInput(
    long PatientId,
    long DepartmentId,
    DateTimeOffset StartAt,
    IReadOnlyCollection<AppointmentLineInput> Services,
    FollowUpBookingInput? FollowUp = null);

public sealed record AppointmentServiceModel(
    long Id,
    long ServiceId,
    string ServiceName,
    long DoctorId,
    string DoctorName,
    int SequenceNumber,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    IReadOnlyCollection<long> DeviceIds);

public sealed record AppointmentModel(
    long Id,
    long PatientId,
    string PatientFileNumber,
    string PatientName,
    string PrimaryPhoneNumber,
    long DepartmentId,
    string DepartmentName,
    long RoomId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    AppointmentStatus Status,
    PaymentStatus PaymentStatus,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    long CreatedByUserId,
    DateTimeOffset CreatedAt,
    long? UpdatedByUserId,
    DateTimeOffset? UpdatedAt,
    string RowVersion,
    IReadOnlyCollection<AppointmentServiceModel> Services);

public sealed record AvailableSlot(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record AppointmentAvailability(
    bool IsAvailable,
    bool WillBeSuspended,
    DateTimeOffset EndAt,
    decimal SubtotalAmount,
    string? Reason,
    IReadOnlyCollection<AvailableSlot> Alternatives);

public sealed record AppointmentPage(
    IReadOnlyCollection<AppointmentModel> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record AppointmentSearch(
    DateTimeOffset? From,
    DateTimeOffset? To,
    long? DepartmentId,
    long? SpecializationId,
    long? ServiceId,
    long? DoctorId,
    AppointmentStatus? Status,
    PaymentStatus? PaymentStatus,
    long? CreatedByUserId,
    string? PatientSearch,
    int? MinimumAge,
    int? MaximumAge,
    PatientGender? Gender,
    string? Area,
    int PageNumber,
    int PageSize);
