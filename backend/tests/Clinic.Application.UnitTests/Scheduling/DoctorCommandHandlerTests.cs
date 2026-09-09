using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Common;
using Clinic.Application.Features.Scheduling.Doctors;

namespace Clinic.Application.UnitTests.Scheduling;

public sealed class DoctorCommandHandlerTests
{
    [Fact]
    public async Task CreateRejectsUnauthenticatedCaller()
    {
        FakeDoctorService service = new();
        CreateDoctorCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<DoctorModel> result = await handler.Handle(
            new CreateDoctorCommand(1, "طبيب", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SchedulingErrors.NotAuthenticated, result.Error);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task CreateNormalizesDoctorInput()
    {
        FakeDoctorService service = new();
        CreateDoctorCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<DoctorModel> result = await handler.Handle(
            new CreateDoctorCommand(2, "  د. منى  ", " 0100 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("د. منى", service.Name);
        Assert.Equal("0100", service.Phone);
    }

    [Fact]
    public async Task ReplaceServicesRejectsInvalidRowVersion()
    {
        FakeDoctorService service = new();
        ReplaceDoctorServicesCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<DoctorModel> result = await handler.Handle(
            new ReplaceDoctorServicesCommand(1, [2], "invalid"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.validation", result.Error.Code);
        Assert.False(service.ReplaceCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeDoctorService : IDoctorAdministrationService
    {
        public bool CreateCalled { get; private set; }
        public bool ReplaceCalled { get; private set; }
        public string? Name { get; private set; }
        public string? Phone { get; private set; }

        public Task<Result<DoctorModel>> CreateAsync(long actorUserId, long departmentId, string name, string? phone, CancellationToken cancellationToken)
        {
            CreateCalled = true; Name = name; Phone = phone;
            return Task.FromResult(Result.Success(Model()));
        }

        public Task<Result<DoctorModel>> UpdateAsync(long actorUserId, long doctorId, string name, string? phone, bool isActive, byte[] rowVersion, CancellationToken cancellationToken) => Task.FromResult(Result.Success(Model()));
        public Task<Result<DoctorModel>> ReplaceServicesAsync(long actorUserId, long doctorId, IReadOnlyCollection<long> serviceIds, byte[] rowVersion, CancellationToken cancellationToken) { ReplaceCalled = true; return Task.FromResult(Result.Success(Model())); }
        public Task<Result> ArchiveAsync(long actorUserId, long doctorId, byte[] rowVersion, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        private static DoctorModel Model() => new(1, 2, "طبيب", null, true, false, [], Convert.ToBase64String(new byte[8]));
    }
}
