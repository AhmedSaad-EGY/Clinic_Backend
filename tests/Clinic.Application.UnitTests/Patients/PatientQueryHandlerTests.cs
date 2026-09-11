using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Application.Features.Patients;

namespace Clinic.Application.UnitTests.Patients;

public sealed class PatientQueryHandlerTests
{
    [Fact]
    public async Task TimelineRejectsInvalidDateRangeBeforeCallingService()
    {
        FakeTimelineService service = new();
        GetPatientTimelineQueryHandler handler = new(new FakeCurrentUser(7), service);
        PatientTimelineFilter filter = new(new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 1), [], false, 1, 20);

        Result<PatientTimelinePage> result = await handler.Handle(
            new GetPatientTimelineQuery(1, false, false, filter), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("patients.validation", result.Error.Code);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task TimelineForwardsValidatedFilters()
    {
        FakeTimelineService service = new();
        GetPatientTimelineQueryHandler handler = new(new FakeCurrentUser(7), service);
        PatientTimelineFilter filter = new(null, null,
            [PatientTimelineRecordType.Payment], false, 1, 20);

        Result<PatientTimelinePage> result = await handler.Handle(
            new GetPatientTimelineQuery(1, false, false, filter), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.WasCalled);
        Assert.Same(filter, service.Filter);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeTimelineService : IPatientTimelineQueryService
    {
        public bool WasCalled { get; private set; }
        public PatientTimelineFilter? Filter { get; private set; }

        public Task<Result<PatientTimelinePage>> GetAsync(long patientId,
            bool includeArchivedPatient, bool includeAdminOnlyNotes,
            PatientTimelineFilter filter, CancellationToken cancellationToken)
        {
            WasCalled = true;
            Filter = filter;
            return Task.FromResult(Result.Success<PatientTimelinePage>(null!));
        }
    }
}
