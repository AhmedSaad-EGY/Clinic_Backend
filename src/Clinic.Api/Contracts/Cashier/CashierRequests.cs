using Clinic.Domain.Cashier;
using Clinic.Domain.Scheduling;

namespace Clinic.Api.Contracts.Cashier;

public sealed record UpdateShiftPolicyRequest(int ClosingGraceMinutes,
    string RowVersion);

public sealed record GenerateShiftsRequest(
    long SecretaryUserId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyCollection<ClinicDayOfWeek>? DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record UpdateShiftRequest(DateOnly Date, TimeOnly StartTime,
    TimeOnly EndTime, string RowVersion);

public sealed record ExtendShiftRequest(DateTimeOffset NewScheduledEnd,
    string Reason, string RowVersion);

public sealed record CancelShiftRequest(string Reason, string RowVersion);

public sealed record OpeningBalanceRequest(decimal Amount, string RowVersion,
    string? Reason = null);

public sealed record ReconcileShiftRequest(decimal DeclaredCash,
    string RowVersion, string? Reason = null);

public sealed record CloseShiftRequest(string RowVersion, string? Reason = null);

public sealed record ShiftSearchRequest(
    long? SecretaryUserId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    ShiftStatus? Status,
    int PageNumber = 1,
    int PageSize = 20);
