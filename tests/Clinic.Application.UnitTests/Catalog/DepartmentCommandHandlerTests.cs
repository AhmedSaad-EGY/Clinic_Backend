using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Catalog.Departments;
using Clinic.Domain.Catalog;

namespace Clinic.Application.UnitTests.Catalog;

public sealed class DepartmentCommandHandlerTests
{
    [Fact]
    public async Task CreateRejectsUnauthenticatedCaller()
    {
        FakeDepartmentService service = new();
        CreateDepartmentCommandHandler handler = new(
            new FakeCurrentUser(null),
            service);

        Result<DepartmentModel> result = await handler.Handle(
            new CreateDepartmentCommand("قسم", null, "غرفة"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.NotAuthenticated, result.Error);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task CreateNormalizesInputAndReturnsCreatedDepartment()
    {
        FakeDepartmentService service = new();
        CreateDepartmentCommandHandler handler = new(
            new FakeCurrentUser(7),
            service);

        Result<DepartmentModel> result = await handler.Handle(
            new CreateDepartmentCommand("  الجلدية ", " وصف ", " غرفة "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.CreateCalled);
        Assert.Equal("الجلدية", service.CreatedName);
        Assert.Equal("وصف", service.CreatedDescription);
        Assert.Equal("غرفة", service.CreatedRoomName);
    }

    [Fact]
    public async Task UpdateReturnsServiceConcurrencyConflict()
    {
        FakeDepartmentService service = new()
        {
            UpdateResult = Result.Failure<DepartmentModel>(
                CatalogErrors.ConcurrencyConflict)
        };
        UpdateDepartmentCommandHandler handler = new(
            new FakeCurrentUser(7),
            service);

        Result<DepartmentModel> result = await handler.Handle(
            new UpdateDepartmentCommand(
                3,
                "قسم",
                null,
                "غرفة",
                Convert.ToBase64String(new byte[8])),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ConcurrencyConflict, result.Error);
    }

    [Fact]
    public async Task CreateRejectsEmptyDepartmentNameBeforeCallingService()
    {
        FakeDepartmentService service = new();
        CreateDepartmentCommandHandler handler = new(
            new FakeCurrentUser(7),
            service);

        Result<DepartmentModel> result = await handler.Handle(
            new CreateDepartmentCommand(" ", null, "غرفة"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("catalog.validation", result.Error.Code);
        Assert.False(service.CreateCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeDepartmentService : IDepartmentCatalogService
    {
        public bool CreateCalled { get; private set; }

        public string? CreatedName { get; private set; }

        public string? CreatedDescription { get; private set; }

        public string? CreatedRoomName { get; private set; }

        public Result<DepartmentModel> UpdateResult { get; init; } =
            Result.Success(CreateModel());

        public Task<Result<DepartmentModel>> CreateAsync(
            long actorUserId,
            string name,
            string? description,
            string roomName,
            CancellationToken cancellationToken)
        {
            CreateCalled = true;
            CreatedName = name;
            CreatedDescription = description;
            CreatedRoomName = roomName;
            return Task.FromResult(Result.Success(CreateModel()));
        }

        public Task<Result<DepartmentModel>> UpdateAsync(
            long actorUserId,
            long departmentId,
            string name,
            string? description,
            string roomName,
            byte[] expectedRowVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(UpdateResult);

        public Task<Result> ArchiveAsync(
            long actorUserId,
            long departmentId,
            byte[] expectedRowVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        private static DepartmentModel CreateModel() => new(
            1,
            "قسم",
            null,
            DepartmentStatus.Active,
            false,
            1,
            "غرفة",
            Convert.ToBase64String(new byte[8]));
    }
}
