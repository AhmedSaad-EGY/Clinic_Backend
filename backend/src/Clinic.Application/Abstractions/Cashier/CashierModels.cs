using Clinic.Domain.Cashier;
using Clinic.Domain.Scheduling;

namespace Clinic.Application.Abstractions.Cashier;

public sealed record GenerateShiftsInput(
    long SecretaryUserId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyCollection<ClinicDayOfWeek> DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record UpdateShiftInput(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record ShiftPolicyModel(int ClosingGraceMinutes, string RowVersion);

public sealed record CashDrawerModel(
    long Id,
    long SecretaryUserId,
    string SecretaryName,
    string Name,
    bool IsActive,
    string RowVersion);

public sealed record ShiftModel(
    long Id,
    long CashDrawerId,
    long SecretaryUserId,
    string SecretaryName,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    DateTimeOffset GraceEndsAt,
    DateTimeOffset? ActualOpenedAt,
    DateTimeOffset? ClosedAt,
    decimal? OpeningBalance,
    decimal? ExpectedCash,
    decimal? DeclaredCash,
    decimal? CashVariance,
    ShiftStatus Status,
    bool IsGraceExpired,
    bool CanEnterOpeningBalance,
    bool CanCollect,
    bool CanReconcile,
    bool CanClose,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string RowVersion);

public sealed record ShiftPage(
    IReadOnlyCollection<ShiftModel> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record CashDrawerPage(
    IReadOnlyCollection<CashDrawerModel> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record GeneratedShifts(
    int CreatedCount,
    IReadOnlyCollection<ShiftModel> Items);

public sealed record ShiftConflictModel(
    long ShiftId,
    DateTimeOffset ScheduledStart,
    DateTimeOffset GraceEndsAt);

public sealed record ShiftSearch(
    long? SecretaryUserId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    ShiftStatus? Status,
    int PageNumber,
    int PageSize);
