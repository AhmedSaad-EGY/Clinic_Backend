using Clinic.Application.Common;
using Clinic.Domain.Appointments;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Domain.Patients;

namespace Clinic.Application.Abstractions.Reporting;

public enum ReportGroupBy
{
    Day = 1,
    Month = 2,
    Department = 3,
    Doctor = 4
}

public sealed record AdminDashboardSummaryModel(
    DateOnly ClinicDate,
    decimal Collected,
    decimal CashCollected,
    decimal ElectronicCollected,
    decimal Refunded,
    decimal NetCollected,
    decimal CashWithdrawn,
    decimal OutstandingAmount,
    int AppointmentCount,
    int BookedCount,
    int ConfirmedCount,
    int CompletedCount,
    int NoShowCount,
    int CancelledCount,
    int SuspendedCount,
    int NewPatientCount,
    int ReturningPatientCount,
    int ActiveShiftCount,
    int ShiftVarianceCount,
    int PendingApprovalCount,
    int PendingWithdrawalCount);

public sealed record FinancialReportFilter(
    DateOnly From,
    DateOnly To,
    long? DepartmentId,
    long? ServiceId,
    long? DoctorId,
    long? SecretaryUserId,
    long? ShiftId,
    long? PaymentMethodId,
    long? PatientId);

public sealed record FinancialBreakdownModel(long? Id, string Name,
    decimal Collected, decimal Refunded, decimal Net);

public sealed record FinancialReportModel(
    DateOnly From,
    DateOnly To,
    decimal Collected,
    decimal Refunded,
    decimal NetCollected,
    decimal CashCollected,
    decimal ElectronicCollected,
    decimal DiscountAmount,
    decimal AppointmentRevenue,
    decimal PackageRevenue,
    decimal CashWithdrawn,
    IReadOnlyCollection<FinancialBreakdownModel> Departments,
    IReadOnlyCollection<FinancialBreakdownModel> PaymentMethods);

public sealed record OperationalReportFilter(
    DateOnly From,
    DateOnly To,
    long? DepartmentId,
    long? ServiceId,
    long? DoctorId,
    AppointmentStatus? Status,
    PatientGender? Gender,
    int? MinimumAge,
    int? MaximumAge,
    string? Area);

public sealed record OperationalBreakdownModel(long? Id, string Name,
    int AppointmentCount, int Quantity, decimal ServiceValue);

public sealed record OperationalReportModel(
    DateOnly From,
    DateOnly To,
    int AppointmentCount,
    int BookedCount,
    int ConfirmedCount,
    int CompletedCount,
    int NoShowCount,
    int CancelledCount,
    int SuspendedCount,
    int TotalServiceQuantity,
    int ConsumedPackageSessionCount,
    int DeviceUsageCount,
    int NewPatientCount,
    int ReturningPatientCount,
    IReadOnlyCollection<OperationalBreakdownModel> Departments,
    IReadOnlyCollection<OperationalBreakdownModel> Doctors);

public sealed record ComparisonReportFilter(DateOnly From, DateOnly To,
    ReportGroupBy GroupBy, long? DepartmentId, long? DoctorId);

public sealed record ComparisonPointModel(string Key, string Label,
    decimal Collected, decimal Refunded, decimal Net, int AppointmentCount);

public sealed record ComparisonReportModel(DateOnly From, DateOnly To,
    ReportGroupBy GroupBy, IReadOnlyCollection<ComparisonPointModel> Points);

public sealed record ShiftReportFilter(DateOnly From, DateOnly To,
    long? SecretaryUserId, ShiftStatus? Status, int PageNumber, int PageSize);

public sealed record ShiftReportItemModel(long ShiftId, long SecretaryUserId,
    string SecretaryName, DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd, ShiftStatus Status, decimal? OpeningBalance,
    decimal CashCollected, decimal ElectronicCollected, decimal CashRefunded,
    decimal ElectronicRefunded, decimal CashWithdrawn, decimal? ExpectedCash,
    decimal? DeclaredCash, decimal? Variance);

public sealed record ShiftReportPage(IReadOnlyCollection<ShiftReportItemModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public sealed record AuditLogFilter(DateOnly From, DateOnly To,
    long? ActorUserId, AuditActorType? ActorType, string? Action,
    string? EntityType, string? EntityId, int PageNumber, int PageSize);

public sealed record AuditLogModel(long Id, long? ActorUserId, string? ActorName,
    AuditActorType ActorType, string Action, string EntityType, string EntityId,
    DateTimeOffset OccurredAt, string? Reason);

public sealed record AuditLogPage(IReadOnlyCollection<AuditLogModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public interface IReportingQueryService
{
    Task<Result<AdminDashboardSummaryModel>> GetDashboardAsync(DateOnly? reportDate,
        CancellationToken cancellationToken);
    Task<Result<FinancialReportModel>> GetFinancialAsync(FinancialReportFilter filter,
        CancellationToken cancellationToken);
    Task<Result<OperationalReportModel>> GetOperationalAsync(
        OperationalReportFilter filter, CancellationToken cancellationToken);
    Task<Result<ComparisonReportModel>> GetComparisonAsync(
        ComparisonReportFilter filter, CancellationToken cancellationToken);
    Task<Result<ShiftReportPage>> GetShiftsAsync(ShiftReportFilter filter,
        CancellationToken cancellationToken);
    Task<Result<AuditLogPage>> GetAuditLogsAsync(AuditLogFilter filter,
        CancellationToken cancellationToken);
}
