namespace Clinic.Application.Abstractions.Cashier;

public sealed record RefundMethodInput(long OriginalAllocationId, decimal Amount,
    string? ReferenceNumber);

public sealed record ExecuteRefundInput(long ApprovalRequestId,
    IReadOnlyCollection<RefundMethodInput> MethodAllocations, string? Note);

public sealed record RefundMethodAllocationModel(long OriginalAllocationId,
    long PaymentMethodId, string Code, string DisplayName, bool IsCash,
    decimal Amount, string? ReferenceNumber);

public sealed record RefundModel(long Id, string TransactionNumber,
    long ApprovalRequestId, long OriginalPaymentId, long AppointmentId,
    long ExecutionShiftId, decimal Amount, long ExecutedByUserId,
    string ExecutedByName, DateTimeOffset ExecutedAt, RefundStatus Status,
    string? Note, IReadOnlyCollection<RefundMethodAllocationModel> MethodAllocations,
    string RowVersion);

public sealed record PostedRefundModel(RefundModel Refund, bool WasReplayed,
    decimal ExpectedCashAfterRefund, bool IsExpectedCashNegative);

public sealed record RefundPage(IReadOnlyCollection<RefundModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public interface IRefundService
{
    Task<Result<PostedRefundModel>> ExecuteAsync(long actorUserId,
        Guid idempotencyKey, ExecuteRefundInput input,
        CancellationToken cancellationToken);
    Task<Result<RefundModel>> GetAsync(long actorUserId, long refundId,
        bool adminOverride, CancellationToken cancellationToken);
    Task<Result<RefundModel>> GetForPatientAsync(long patientId, long refundId,
        CancellationToken cancellationToken);
    Task<Result<RefundPage>> ListForShiftAsync(long actorUserId, long shiftId,
        bool adminOverride, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
}
