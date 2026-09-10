using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Discounts;
using Clinic.Domain.Discounts;

namespace Clinic.Application.UnitTests;

public sealed class DiscountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 10, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public async Task CreateRejectsUnauthenticatedCallerBeforePersistence()
    {
        FakeDiscountService service = new();
        CreateDiscountCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<DiscountModel> result = await handler.Handle(new CreateDiscountCommand(
            Definition()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task CreateRejectsDuplicateTargetsBeforePersistence()
    {
        FakeDiscountService service = new();
        CreateDiscountCommandHandler handler = new(new FakeCurrentUser(1), service);
        DiscountDefinition definition = Definition() with
        {
            ScopeMode = DiscountScopeMode.Selected,
            Targets = new DiscountTargetInput([2, 2], [], [])
        };

        Result<DiscountModel> result = await handler.Handle(
            new CreateDiscountCommand(definition), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task UpdateRejectsInvalidRowVersionBeforePersistence()
    {
        FakeDiscountService service = new();
        UpdateDiscountCommandHandler handler = new(new FakeCurrentUser(1), service);

        Result<DiscountModel> result = await handler.Handle(new UpdateDiscountCommand(1,
            Definition(), "invalid"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.UpdateCalled);
    }

    private static DiscountDefinition Definition() => new("خصم", DiscountType.Percentage,
        10m, DiscountAppliesTo.Both, DiscountScopeMode.All, Now, Now.AddDays(1),
        new DiscountTargetInput([], [], []));

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeDiscountService : IDiscountService
    {
        public bool CreateCalled { get; private set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<DiscountModel>> CreateAsync(long actorUserId,
            DiscountDefinition definition, CancellationToken cancellationToken)
        {
            CreateCalled = true;
            return Task.FromResult(Result.Failure<DiscountModel>(DiscountErrors.NotFound));
        }

        public Task<Result<DiscountModel>> UpdateAsync(long actorUserId, long discountId,
            DiscountDefinition definition, byte[] expectedRowVersion,
            CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            return Task.FromResult(Result.Failure<DiscountModel>(DiscountErrors.NotFound));
        }

        public Task<Result<DiscountModel>> SetActiveAsync(long actorUserId, long discountId,
            bool isActive, byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<DiscountModel>(DiscountErrors.NotFound));

        public Task<Result> ArchiveAsync(long actorUserId, long discountId,
            byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(DiscountErrors.NotFound));

        public Task<Result<DiscountModel>> GetAsync(long discountId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<DiscountModel>(DiscountErrors.NotFound));

        public Task<Result<DiscountPage>> SearchAsync(DiscountFilter filter,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<DiscountPage>(DiscountErrors.NotFound));
    }
}
