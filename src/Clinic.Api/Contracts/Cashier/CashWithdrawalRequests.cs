using Clinic.Domain.Cashier;

namespace Clinic.Api.Contracts.Cashier;

public sealed record CreateCashWithdrawalRequest(decimal Amount, string Reason);

public sealed record ReviewCashWithdrawalRequest(string Reason,
    string RowVersion);

public sealed record CashWithdrawalRowVersionRequest(string RowVersion);

public sealed record CashWithdrawalSearchRequest(
    long? ShiftId,
    long? SecretaryUserId,
    CashWithdrawalStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber = 1,
    int PageSize = 20);
