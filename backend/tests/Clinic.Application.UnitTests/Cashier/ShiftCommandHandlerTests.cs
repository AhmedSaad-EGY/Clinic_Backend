using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;
using Clinic.Domain.Scheduling;

namespace Clinic.Application.UnitTests.Cashier;

public sealed class ShiftCommandHandlerTests
{
    [Fact]
    public async Task GenerationRejectsUnauthenticatedUser()
    {
        FakeShiftService service = new();
        GenerateShiftsCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<GeneratedShifts> result = await handler.Handle(
            new GenerateShiftsCommand(ValidGeneration()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CashierErrors.NotAuthenticated, result.Error);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task GenerationRejectsMoreThanNinetyDays()
    {
        FakeShiftService service = new();
        GenerateShiftsCommandHandler handler = new(new FakeCurrentUser(1), service);
        GenerateShiftsInput input = ValidGeneration() with
        {
            ToDate = new DateOnly(2027, 4, 1)
        };

        Result<GeneratedShifts> result = await handler.Handle(
            new GenerateShiftsCommand(input), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task AdminOverrideRequiresReason()
    {
        FakeShiftService service = new();
        RecordOpeningBalanceCommandHandler handler = new(
            new FakeCurrentUser(1), service);

        Result<ShiftModel> result = await handler.Handle(
            new RecordOpeningBalanceCommand(1, 0, AdminOverride: true,
                Reason: null, Convert.ToBase64String(new byte[8])),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ValidGenerationCallsInfrastructure()
    {
        FakeShiftService service = new();
        GenerateShiftsCommandHandler handler = new(new FakeCurrentUser(1), service);

        Result<GeneratedShifts> result = await handler.Handle(
            new GenerateShiftsCommand(ValidGeneration()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.WasCalled);
    }

    private static GenerateShiftsInput ValidGeneration() => new(
        2,
        new DateOnly(2027, 1, 1),
        new DateOnly(2027, 1, 31),
        [ClinicDayOfWeek.Sunday, ClinicDayOfWeek.Tuesday],
        new TimeOnly(10, 0),
        new TimeOnly(14, 0));

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeShiftService : IShiftService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<GeneratedShifts>> GenerateAsync(long actorUserId,
            GenerateShiftsInput input, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result.Success(new GeneratedShifts(0, [])));
        }

        public Task<Result<ShiftPolicyModel>> GetPolicyAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftPolicyModel>> UpdatePolicyAsync(long actorUserId,
            int closingGraceMinutes, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<CashDrawerPage>> ListDrawersAsync(int pageNumber, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftModel>> UpdateAsync(long actorUserId, long shiftId,
            UpdateShiftInput input, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftModel>> ExtendAsync(long actorUserId, long shiftId,
            DateTimeOffset newScheduledEnd, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> CancelAsync(long actorUserId, long shiftId, string reason,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<ShiftModel>> RecordOpeningBalanceAsync(long actorUserId,
            long shiftId, decimal amount, bool adminOverride, string? reason,
            byte[] rowVersion, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Infrastructure should not be called.");
        }
        public Task<Result<ShiftModel>> ReconcileAsync(long actorUserId, long shiftId,
            decimal declaredCash, bool adminOverride, string? reason,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<ShiftModel>> CloseAsync(long actorUserId, long shiftId,
            bool adminOverride, string? reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftModel>> GetAsync(long shiftId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftModel>> GetCurrentAsync(long actorUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftPage>> SearchAsync(ShiftSearch search,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ShiftPage>> HistoryAsync(long actorUserId, int pageNumber,
            int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
