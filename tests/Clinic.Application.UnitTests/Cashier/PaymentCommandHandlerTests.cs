using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;

namespace Clinic.Application.UnitTests.Cashier;

public sealed class PaymentCommandHandlerTests
{
    [Fact]
    public async Task PaymentRejectsUnauthenticatedUser()
    {
        FakePaymentService service = new();
        PostPaymentCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<PostedPaymentModel> result = await handler.Handle(
            new PostPaymentCommand(Guid.NewGuid(), ValidInput(), false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task SecretaryCannotSelectShift()
    {
        FakePaymentService service = new();
        PostPaymentCommandHandler handler = new(new FakeCurrentUser(3), service);

        Result<PostedPaymentModel> result = await handler.Handle(
            new PostPaymentCommand(Guid.NewGuid(), ValidInput() with { ShiftId = 10 }, false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task AdminOverrideRequiresShiftAndReason()
    {
        FakePaymentService service = new();
        PostPaymentCommandHandler handler = new(new FakeCurrentUser(1), service);

        Result<PostedPaymentModel> result = await handler.Handle(
            new PostPaymentCommand(Guid.NewGuid(), ValidInput() with { ShiftId = 10 }, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ValidPaymentCallsService()
    {
        FakePaymentService service = new();
        PostPaymentCommandHandler handler = new(new FakeCurrentUser(3), service);

        Result<PostedPaymentModel> result = await handler.Handle(
            new PostPaymentCommand(Guid.NewGuid(), ValidInput(), false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.WasCalled);
    }

    private static PostPaymentInput ValidInput() => new(null, [10],
        [new PaymentMethodInput(1, 100m, null)], null, null);

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakePaymentService : IPaymentService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<PostedPaymentModel>> PostAsync(long actorUserId,
            Guid idempotencyKey, PostPaymentInput input, bool adminOverride,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result.Success(new PostedPaymentModel(null!, false)));
        }

        public Task<Result<IReadOnlyCollection<PaymentMethodModel>>> ListMethodsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<PaymentModel>> GetAsync(long actorUserId, long paymentId,
            bool adminOverride, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<PaymentPage>> ListForShiftAsync(long actorUserId, long shiftId,
            bool adminOverride, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftCollectionSummaryModel>> GetShiftSummaryAsync(
            long actorUserId, long shiftId, bool adminOverride,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
