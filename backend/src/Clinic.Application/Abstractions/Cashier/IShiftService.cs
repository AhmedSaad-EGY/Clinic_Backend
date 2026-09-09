using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Cashier;

public interface IShiftService
{
    Task<Result<ShiftPolicyModel>> GetPolicyAsync(CancellationToken cancellationToken);
    Task<Result<ShiftPolicyModel>> UpdatePolicyAsync(long actorUserId,
        int closingGraceMinutes, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<CashDrawerPage>> ListDrawersAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<Result<GeneratedShifts>> GenerateAsync(long actorUserId,
        GenerateShiftsInput input, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> UpdateAsync(long actorUserId, long shiftId,
        UpdateShiftInput input, byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> ExtendAsync(long actorUserId, long shiftId,
        DateTimeOffset newScheduledEnd, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result> CancelAsync(long actorUserId, long shiftId, string reason,
        byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> RecordOpeningBalanceAsync(long actorUserId,
        long shiftId, decimal amount, bool adminOverride, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> ReconcileAsync(long actorUserId, long shiftId,
        decimal declaredCash, bool adminOverride, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> CloseAsync(long actorUserId, long shiftId,
        bool adminOverride, string? reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<ShiftModel>> GetAsync(long shiftId, CancellationToken cancellationToken);
    Task<Result<ShiftModel>> GetCurrentAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result<ShiftPage>> SearchAsync(ShiftSearch search,
        CancellationToken cancellationToken);
    Task<Result<ShiftPage>> HistoryAsync(long actorUserId, int pageNumber,
        int pageSize, CancellationToken cancellationToken);
}
