using Clinic.Domain.Approvals;
using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Cashier;

public sealed record RefundableMethodModel(long OriginalAllocationId,
    long PaymentMethodId, string Code, string DisplayName, bool IsCash,
    decimal RemainingAmount);

public sealed record ApprovalRequestModel(long Id, ApprovalRequestType RequestType,
    long AppointmentId, long PatientId, string PatientName, long? OriginalPaymentId,
    string? OriginalPaymentNumber, decimal? RequestedAmount,
    long RequestedByUserId, string RequestedByName, DateTimeOffset RequestedAt,
    string RequestNote, ApprovalRequestStatus Status,
    long? ReviewedByAdminUserId, string? ReviewedByAdminName,
    DateTimeOffset? ReviewedAt, string? DecisionReason,
    bool RequiresRefundExecution, long? RefundId,
    IReadOnlyCollection<RefundableMethodModel> RefundableMethods,
    string RowVersion);

public sealed record ApprovalRequestPage(
    IReadOnlyCollection<ApprovalRequestModel> Items, int PageNumber,
    int PageSize, int TotalCount);

public sealed record ApprovalRequestSearch(ApprovalRequestStatus? Status,
    bool AwaitingRefundOnly, int PageNumber, int PageSize);

public interface IApprovalRequestService
{
    Task<Result<ApprovalRequestModel>> CreateCancellationAsync(long actorUserId,
        long appointmentId, string reason, byte[] appointmentRowVersion,
        CancellationToken cancellationToken);
    Task<Result<ApprovalRequestModel>> ApproveAsync(long actorUserId,
        long requestId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<ApprovalRequestModel>> RejectAsync(long actorUserId,
        long requestId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result<ApprovalRequestModel>> GetAsync(long requestId,
        CancellationToken cancellationToken);
    Task<Result<ApprovalRequestPage>> SearchAsync(ApprovalRequestSearch search,
        CancellationToken cancellationToken);
}
