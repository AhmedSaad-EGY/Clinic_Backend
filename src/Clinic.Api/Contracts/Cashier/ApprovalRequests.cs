namespace Clinic.Api.Contracts.Cashier;

public sealed record CreateCancellationApprovalRequest(long AppointmentId,
    string AppointmentRowVersion, string Reason);

public sealed record ReviewApprovalRequest(string RowVersion, string Reason);

public sealed record RefundMethodRequest(long OriginalAllocationId,
    decimal Amount, string? ReferenceNumber);

public sealed record ExecuteRefundRequest(
    IReadOnlyCollection<RefundMethodRequest> MethodAllocations,
    string? Note = null);
