using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Common;
using Clinic.Application.Features.Scheduling.Closures;
using Clinic.Application.Features.Scheduling.Exceptions;
using Clinic.Application.Features.Scheduling.Schedules;
using Clinic.Domain.Scheduling;

namespace Clinic.Application.UnitTests.Scheduling;

public sealed class SchedulingCommandValidationTests
{
    private static readonly FakeCurrentUser Admin = new(7);

    [Fact]
    public async Task CreateScheduleRejectsInvalidRangeBeforeCallingService()
    {
        FakeScheduleService service = new();
        CreateDoctorScheduleCommandHandler handler = new(Admin, service);

        Result<DoctorScheduleModel> result = await handler.Handle(
            new CreateDoctorScheduleCommand(
                1,
                ClinicDayOfWeek.Monday,
                new TimeOnly(12, 0),
                new TimeOnly(11, 0),
                new DateOnly(2026, 9, 7),
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.validation", result.Error.Code);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task CreateExceptionRejectsOneSidedTimeBeforeCallingService()
    {
        FakeScheduleService service = new();
        CreateDoctorExceptionCommandHandler handler = new(Admin, service);

        Result<DoctorExceptionModel> result = await handler.Handle(
            new CreateDoctorExceptionCommand(
                1,
                new DateOnly(2026, 9, 7),
                new TimeOnly(10, 0),
                null,
                DoctorExceptionType.Unavailable,
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.validation", result.Error.Code);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task CreateClosureRejectsInvalidRangeBeforeCallingService()
    {
        FakeScheduleService service = new();
        CreateDepartmentClosureCommandHandler handler = new(Admin, service);
        DateTimeOffset instant = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        Result<DepartmentClosureModel> result = await handler.Handle(
            new CreateDepartmentClosureCommand(1, instant, instant, "صيانة"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.validation", result.Error.Code);
        Assert.False(service.WasCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeScheduleService : IScheduleAdministrationService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<DoctorScheduleModel>> CreateScheduleAsync(long actorUserId, long doctorId, ClinicDayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("The service should not be called.");
        }

        public Task<Result<DoctorScheduleModel>> UpdateScheduleAsync(long actorUserId, long scheduleId, ClinicDayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime, DateOnly effectiveFrom, DateOnly? effectiveTo, byte[] rowVersion, bool confirmAffectedAppointments, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateScheduleAsync(long actorUserId, long scheduleId, byte[] rowVersion, bool confirmAffectedAppointments, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<DoctorExceptionModel>> CreateExceptionAsync(long actorUserId, long doctorId, DateOnly exceptionDate, TimeOnly? startTime, TimeOnly? endTime, DoctorExceptionType type, string? reason, bool confirmAffectedAppointments, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("The service should not be called.");
        }

        public Task<Result> CancelExceptionAsync(long actorUserId, long exceptionId, byte[] rowVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<DepartmentClosureModel>> CreateClosureAsync(long actorUserId, long departmentId, DateTimeOffset startAt, DateTimeOffset endAt, string reason, bool confirmAffectedAppointments, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("The service should not be called.");
        }

        public Task<Result> CancelClosureAsync(long actorUserId, long closureId, byte[] rowVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
