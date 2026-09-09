using Clinic.Domain.Approvals;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Cashier;

public sealed class ApprovalRequestDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PaidCancellationCarriesTheFullServerCalculatedAmount()
    {
        ApprovalRequest request = ApprovalRequest.CreateAppointmentCancellation(
            10, 20, 300m, 30, Now, "طلب المريض");

        Assert.True(request.RequiresRefund);
        Assert.Equal(300m, request.RequestedAmount);
        Assert.Equal(ApprovalRequestStatus.Pending, request.Status);
    }

    [Fact]
    public void ApprovalIsImmutableAndRequiresAnotherUser()
    {
        ApprovalRequest request = ApprovalRequest.CreateAppointmentCancellation(
            10, null, null, 30, Now, "طلب المريض");

        Assert.Throws<DomainException>(() => request.Approve(30, Now, "موافقة"));
        request.Approve(1, Now, "موافقة");
        Assert.Throws<DomainException>(() => request.Reject(1, Now, "تغيير القرار"));
    }

    [Fact]
    public void PaymentAndRefundAmountMustExistTogether()
    {
        Assert.Throws<DomainException>(() =>
            ApprovalRequest.CreateAppointmentCancellation(10, 20, null, 30,
                Now, "طلب المريض"));
    }
}
