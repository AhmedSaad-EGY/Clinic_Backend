using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Features.Packages;

namespace Clinic.Application.UnitTests.Packages;

public sealed class PackageCommandHandlerTests
{
    [Fact]
    public async Task CreateRejectsUnauthenticatedCallerBeforePersistence()
    {
        FakeCommandService service = new();
        CreatePackageCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<PackageModel> result = await handler.Handle(new CreatePackageCommand(1,
            "باقة", 100m, 14, 90, [new PackageServiceInput(2, 1, 100m)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PackageErrors.NotAuthenticated, result.Error);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task CreateRejectsDuplicateServicesBeforePersistence()
    {
        FakeCommandService service = new();
        CreatePackageCommandHandler handler = new(new FakeCurrentUser(7), service);
        PackageServiceInput line = new(2, 1, 100m);

        Result<PackageModel> result = await handler.Handle(new CreatePackageCommand(1,
            "باقة", 100m, 14, 90, [line, line]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("packages.validation", result.Error.Code);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task UpdateRejectsInvalidRowVersionBeforePersistence()
    {
        FakeCommandService service = new();
        UpdatePackageCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PackageModel> result = await handler.Handle(new UpdatePackageCommand(1,
            "باقة", 100m, 14, 90, [new PackageServiceInput(2, 1, 100m)], "invalid"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.UpdateCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeCommandService : IPackageCommandService
    {
        public bool CreateCalled { get; private set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<PackageModel>> CreateAsync(long actorUserId, long departmentId,
            string name, decimal basePrice, int activationGraceDays, int usageDurationDays,
            IReadOnlyCollection<PackageServiceInput> services,
            CancellationToken cancellationToken)
        {
            CreateCalled = true;
            return Task.FromResult(Result.Failure<PackageModel>(PackageErrors.NotFound));
        }

        public Task<Result<PackageModel>> UpdateAsync(long actorUserId, long packageId,
            string name, decimal basePrice, int activationGraceDays, int usageDurationDays,
            IReadOnlyCollection<PackageServiceInput> services,
            byte[] expectedRowVersion, CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            return Task.FromResult(Result.Failure<PackageModel>(PackageErrors.NotFound));
        }

        public Task<Result<PackageModel>> SetActiveAsync(long actorUserId, long packageId,
            bool isActive, byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<PackageModel>(PackageErrors.NotFound));

        public Task<Result> ArchiveAsync(long actorUserId, long packageId,
            byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(PackageErrors.NotFound));
    }
}
