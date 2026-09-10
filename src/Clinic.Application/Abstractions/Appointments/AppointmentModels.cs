using Clinic.Domain.Appointments;
using Clinic.Domain.Packages;
using Clinic.Domain.Patients;

namespace Clinic.Application.Abstractions.Appointments;

public sealed record AppointmentLineInput(
    long ServiceId,
    long DoctorId,
    int Quantity,
    IReadOnlyCollection<long> OptionalDeviceIds);

public sealed record FollowUpBookingInput(long FollowUpId, string RowVersion);

public sealed record DiscountOverrideInput(DiscountOverrideMode Mode, long? DiscountId,
    string? Reason);

public sealed record AppointmentInput(
    long PatientId,
    long DepartmentId,
    DateTimeOffset StartAt,
    IReadOnlyCollection<AppointmentLineInput> Services,
    FollowUpBookingInput? FollowUp = null,
    long? PatientPackageId = null,
    Guid? IdempotencyKey = null,
    DiscountOverrideInput? DiscountOverride = null);

public sealed record PackageSessionBookingModel(long Id, long PackageSessionId,
    int SequenceNumber, PackageSessionBookingStatus Status, DateTimeOffset ReservedAt,
    DateTimeOffset? ReleasedAt, DateTimeOffset? ConsumedAt);

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
    IReadOnlyCollection<long> DeviceIds,
    decimal PackageCoveredAmount,
    PackageSessionBookingModel? PackageSessionBooking,
    long? DiscountId = null,
    decimal DiscountAmount = 0,
    DiscountOverrideMode? DiscountOverrideMode = null,
    long? DiscountOverrideByAdminUserId = null,
    string? DiscountOverrideReason = null);

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
    IReadOnlyCollection<AppointmentServiceModel> Services,
    long? PatientPackageId,
    decimal PackageCoveredAmount,
    bool WasReplayed = false);

public sealed record AvailableSlot(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record AppointmentAvailability(
    bool IsAvailable,
    bool WillBeSuspended,
    DateTimeOffset EndAt,
    decimal SubtotalAmount,
    string? Reason,
    IReadOnlyCollection<AvailableSlot> Alternatives,
    decimal DiscountAmount = 0,
    decimal NetAmount = 0);

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
