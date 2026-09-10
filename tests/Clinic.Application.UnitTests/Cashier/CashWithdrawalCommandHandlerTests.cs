using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;

namespace Clinic.Application.UnitTests.Cashier;

public sealed class CashWithdrawalCommandHandlerTests
{
    [Fact]
    public async Task ValidRequestCallsService()
    {
        FakeCashWithdrawalService service = new();
        CreateCashWithdrawalCommandHandler handler = new(new FakeCurrentUser(10),
            service);

        Result<CreatedCashWithdrawalModel> result = await handler.Handle(
            new CreateCashWithdrawalCommand(Guid.NewGuid(), 50,
                "شراء مستلزمات"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.CreateWasCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(10000000000000000L)]
    public async Task InvalidAmountDoesNotCallService(decimal amount)
    {
        FakeCashWithdrawalService service = new();
        CreateCashWithdrawalCommandHandler handler = new(new FakeCurrentUser(10),
            service);

        Result<CreatedCashWithdrawalModel> result = await handler.Handle(
            new CreateCashWithdrawalCommand(Guid.NewGuid(), amount, "سبب"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.CreateWasCalled);
    }

    [Fact]
    public async Task ExecuteRequiresValidIdempotencyKeyAndRowVersion()
    {
        FakeCashWithdrawalService service = new();
        ExecuteCashWithdrawalCommandHandler handler = new(new FakeCurrentUser(10),
            service);

        Result<ExecutedCashWithdrawalModel> result = await handler.Handle(
            new ExecuteCashWithdrawalCommand(1, Guid.Empty,
                Convert.ToBase64String(new byte[8])), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.ExecuteWasCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeCashWithdrawalService : ICashWithdrawalService
    {
        public bool CreateWasCalled { get; private set; }
        public bool ExecuteWasCalled { get; private set; }

        public Task<Result<CreatedCashWithdrawalModel>> CreateAsync(
            long actorUserId, Guid idempotencyKey, decimal amount, string reason,
            CancellationToken cancellationToken)
        {
            CreateWasCalled = true;
            return Task.FromResult(Result.Success(
                new CreatedCashWithdrawalModel(null!, false)));
        }

        public Task<Result<ExecutedCashWithdrawalModel>> ExecuteAsync(
            long actorUserId, long withdrawalId, Guid idempotencyKey,
            byte[] rowVersion, CancellationToken cancellationToken)
        {
            ExecuteWasCalled = true;
            return Task.FromResult(Result.Success(
                new ExecutedCashWithdrawalModel(null!, false, 0)));
        }

        public Task<Result<CashWithdrawalModel>> ApproveAsync(long actorUserId,
            long withdrawalId, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashWithdrawalModel>> RejectAsync(long actorUserId,
            long withdrawalId, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashWithdrawalModel>> CancelAsync(long actorUserId,
            long withdrawalId, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashWithdrawalModel>> GetAsync(long actorUserId,
            long withdrawalId, bool adminOverride,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashWithdrawalPage>> ListForShiftAsync(long actorUserId,
            long shiftId, bool adminOverride, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashWithdrawalPage>> SearchAsync(
            CashWithdrawalSearch search,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
