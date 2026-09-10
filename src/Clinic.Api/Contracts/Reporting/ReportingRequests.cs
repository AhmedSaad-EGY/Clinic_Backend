using Clinic.Application.Abstractions.Reporting;
using Clinic.Domain.Appointments;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Domain.Patients;

namespace Clinic.Api.Contracts.Reporting;

public sealed record FinancialReportRequest(DateOnly From, DateOnly To,
    long? DepartmentId, long? ServiceId, long? DoctorId,
    long? SecretaryUserId, long? ShiftId, long? PaymentMethodId, long? PatientId);

public sealed record OperationalReportRequest(DateOnly From, DateOnly To,
    long? DepartmentId, long? ServiceId, long? DoctorId,
    AppointmentStatus? Status, PatientGender? Gender, int? MinimumAge,
    int? MaximumAge, string? Area);

public sealed record ComparisonReportRequest(DateOnly From, DateOnly To,
    ReportGroupBy GroupBy, long? DepartmentId, long? DoctorId);

public sealed record ShiftReportRequest(DateOnly From, DateOnly To,
    long? SecretaryUserId, ShiftStatus? Status, int PageNumber = 1,
    int PageSize = 20);

public sealed record AuditLogRequest(DateOnly From, DateOnly To,
    long? ActorUserId, AuditActorType? ActorType, string? Action,
    string? EntityType, string? EntityId, int PageNumber = 1,
    int PageSize = 20);
