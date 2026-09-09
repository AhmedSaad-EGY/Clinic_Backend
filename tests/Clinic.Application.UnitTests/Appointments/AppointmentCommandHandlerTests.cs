using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Appointments;

namespace Clinic.Application.UnitTests.Appointments;

public sealed class AppointmentCommandHandlerTests
{
    [Fact]
    public async Task CreateRejectsUnauthenticatedUserBeforeCallingInfrastructure()
    {
        FakeAppointmentService service = new();
        CreateAppointmentCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<AppointmentModel> result = await handler.Handle(new CreateAppointmentCommand(
            ValidInput()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AppointmentErrors.NotAuthenticated, result.Error);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task CreateRejectsStartOutsideFifteenMinuteGrid()
    {
        FakeAppointmentService service = new();
        CreateAppointmentCommandHandler handler = new(new FakeCurrentUser(1), service);
        AppointmentInput input = ValidInput() with
        {
            StartAt = new DateTimeOffset(2027, 1, 1, 10, 7, 0, TimeSpan.Zero)
        };

        Result<AppointmentModel> result = await handler.Handle(
            new CreateAppointmentCommand(input), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    private static AppointmentInput ValidInput() => new(1, 2,
        new DateTimeOffset(2027, 1, 1, 10, 0, 0, TimeSpan.Zero),
        [new AppointmentLineInput(3, 4, 1, [])]);

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeAppointmentService : IAppointmentService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<AppointmentModel>> CreateAsync(long actorUserId,
            AppointmentInput input, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Infrastructure should not be called.");
        }

        public Task<Result<AppointmentAvailability>> CheckAvailabilityAsync(long actorUserId,
            AppointmentInput input, long? excludedAppointmentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<AppointmentModel>> UpdateAsync(long actorUserId, long appointmentId,
            AppointmentInput input, byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result> CancelAsync(long actorUserId, long appointmentId, string? reason,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result> CompleteAsync(long actorUserId, long appointmentId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result> MarkNoShowAsync(long actorUserId, long appointmentId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<AppointmentModel>> GetAsync(long appointmentId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IReadOnlyCollection<AppointmentModel>>> CalendarAsync(
            DateOnly clinicDate, long? departmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<AppointmentPage>> SearchAsync(AppointmentSearch search,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<int>> RevalidateSuspendedAsync(long? actorUserId,
            long? departmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<AppointmentModel>> TransferDoctorAsync(long actorUserId,
            long appointmentId, long appointmentServiceId, long doctorId, string reason,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
