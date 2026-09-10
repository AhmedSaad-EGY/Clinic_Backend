using Clinic.Application.Common;
using Clinic.Domain.Cashier;

namespace Clinic.Application.Abstractions.Cashier;

public sealed record CashWithdrawalModel(
    long Id,
    string WithdrawalNumber,
    long ShiftId,
    decimal Amount,
    string Reason,
    CashWithdrawalStatus Status,
    long RequestedByUserId,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    long? ReviewedByAdminUserId,
    string? ReviewedByAdminName,
    DateTimeOffset? ReviewedAt,
    string? DecisionReason,
    long? ExecutedByUserId,
    string? ExecutedByName,
    DateTimeOffset? ExecutedAt,
    long? CancelledByUserId,
    string? CancelledByUserName,
    DateTimeOffset? CancelledAt,
    string RowVersion);

public sealed record CreatedCashWithdrawalModel(CashWithdrawalModel Withdrawal,
    bool WasReplayed);

public sealed record ExecutedCashWithdrawalModel(CashWithdrawalModel Withdrawal,
    bool WasReplayed, decimal CurrentExpectedCash);

public sealed record CashWithdrawalPage(
    IReadOnlyCollection<CashWithdrawalModel> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record CashWithdrawalSearch(
    long? ShiftId,
    long? SecretaryUserId,
    CashWithdrawalStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber,
    int PageSize);

public interface ICashWithdrawalService
{
    Task<Result<CreatedCashWithdrawalModel>> CreateAsync(long actorUserId,
        Guid idempotencyKey, decimal amount, string reason,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalModel>> ApproveAsync(long actorUserId,
        long withdrawalId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalModel>> RejectAsync(long actorUserId,
        long withdrawalId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalModel>> CancelAsync(long actorUserId,
        long withdrawalId, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<ExecutedCashWithdrawalModel>> ExecuteAsync(long actorUserId,
        long withdrawalId, Guid idempotencyKey, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalModel>> GetAsync(long actorUserId,
        long withdrawalId, bool adminOverride,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalPage>> ListForShiftAsync(long actorUserId,
        long shiftId, bool adminOverride, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<Result<CashWithdrawalPage>> SearchAsync(CashWithdrawalSearch search,
        CancellationToken cancellationToken);
}
