using Clinic.Domain.Common;

namespace Clinic.Domain.Approvals;

public sealed class ApprovalRequest : AggregateRoot
{
    private ApprovalRequest() { }

    private ApprovalRequest(long appointmentId, long? originalPaymentId,
        decimal? requestedAmount, long requestedByUserId,
        DateTimeOffset requestedAt, string requestNote)
    {
        if (appointmentId <= 0 || requestedByUserId <= 0)
        {
            throw new DomainException("بيانات طلب الموافقة غير صحيحة.");
        }

        if (originalPaymentId.HasValue != requestedAmount.HasValue ||
            originalPaymentId is <= 0 || requestedAmount is <= 0)
        {
            throw new DomainException("بيانات التحصيل الأصلي ومبلغ الاسترداد غير متطابقة.");
        }

        AppointmentId = appointmentId;
        OriginalPaymentId = originalPaymentId;
        RequestedAmount = requestedAmount;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        RequestNote = RequiredText(requestNote, "سبب طلب الإلغاء");
        RequestType = ApprovalRequestType.AppointmentCancellation;
        Status = ApprovalRequestStatus.Pending;
    }

    public ApprovalRequestType RequestType { get; private set; }
    public long AppointmentId { get; private set; }
    public long? OriginalPaymentId { get; private set; }
    public decimal? RequestedAmount { get; private set; }
    public long RequestedByUserId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public string RequestNote { get; private set; } = string.Empty;
    public ApprovalRequestStatus Status { get; private set; }
    public long? ReviewedByAdminUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool RequiresRefund => RequestedAmount.HasValue;

    public static ApprovalRequest CreateAppointmentCancellation(long appointmentId,
        long? originalPaymentId, decimal? requestedAmount, long requestedByUserId,
        DateTimeOffset requestedAt, string requestNote) => new(appointmentId,
            originalPaymentId, requestedAmount, requestedByUserId, requestedAt,
            requestNote);

    public void Approve(long adminUserId, DateTimeOffset reviewedAt, string reason) =>
        Review(adminUserId, reviewedAt, reason, ApprovalRequestStatus.Approved);

    public void Reject(long adminUserId, DateTimeOffset reviewedAt, string reason) =>
        Review(adminUserId, reviewedAt, reason, ApprovalRequestStatus.Rejected);

    private void Review(long adminUserId, DateTimeOffset reviewedAt, string reason,
        ApprovalRequestStatus status)
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            throw new DomainException("تمت مراجعة طلب الموافقة بالفعل.");
        }

        if (adminUserId <= 0 || adminUserId == RequestedByUserId)
        {
            throw new DomainException("مراجع طلب الموافقة غير صحيح.");
        }

        ReviewedByAdminUserId = adminUserId;
        ReviewedAt = reviewedAt;
        DecisionReason = RequiredText(reason, "سبب قرار الأدمن");
        Status = status;
    }

    private static string RequiredText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 500)
        {
            throw new DomainException($"{field} مطلوب ويجب ألا يتجاوز 500 حرف.");
        }

        return value.Trim();
    }
}
