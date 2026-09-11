using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;

namespace Clinic.Application.UnitTests.Cashier;

public sealed class ApprovalCommandHandlerTests
{
    [Fact]
    public async Task CancellationRequestRequiresReasonAndRowVersion()
    {
        FakeApprovalService service = new();
        CreateCancellationApprovalCommandHandler handler = new(
            new FakeCurrentUser(3), service);

        Result<ApprovalRequestModel> result = await handler.Handle(
            new CreateCancellationApprovalCommand(10, "", "invalid"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ValidCancellationRequestCallsService()
    {
        FakeApprovalService service = new();
        CreateCancellationApprovalCommandHandler handler = new(
            new FakeCurrentUser(3), service);

        Result<ApprovalRequestModel> result = await handler.Handle(
            new CreateCancellationApprovalCommand(10, "طلب المريض",
                Convert.ToBase64String(new byte[8])), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.WasCalled);
    }

    [Fact]
    public async Task RefundRejectsAnEmptyIdempotencyKey()
    {
        FakeRefundService service = new();
        ExecuteRefundCommandHandler handler = new(new FakeCurrentUser(3), service);

        Result<PostedRefundModel> result = await handler.Handle(
            new ExecuteRefundCommand(Guid.Empty, new ExecuteRefundInput(1,
                [new RefundMethodInput(2, 100m, null)], null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeApprovalService : IApprovalRequestService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<ApprovalRequestModel>> CreateCancellationAsync(
            long actorUserId, long appointmentId, string reason,
            byte[] appointmentRowVersion, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result.Success<ApprovalRequestModel>(null!));
        }

        public Task<Result<ApprovalRequestModel>> ApproveAsync(long actorUserId,
            long requestId, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ApprovalRequestModel>> RejectAsync(long actorUserId,
            long requestId, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ApprovalRequestModel>> GetAsync(long requestId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ApprovalRequestPage>> SearchAsync(ApprovalRequestSearch search,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRefundService : IRefundService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<PostedRefundModel>> ExecuteAsync(long actorUserId,
            Guid idempotencyKey, ExecuteRefundInput input,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result.Success<PostedRefundModel>(null!));
        }

        public Task<Result<RefundModel>> GetAsync(long actorUserId, long refundId,
            bool adminOverride, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<RefundModel>> GetForPatientAsync(long patientId, long refundId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<RefundPage>> ListForShiftAsync(long actorUserId,
            long shiftId, bool adminOverride, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
