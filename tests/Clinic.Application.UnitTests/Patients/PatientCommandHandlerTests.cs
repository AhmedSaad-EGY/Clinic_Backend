using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Application.Features.Patients;
using Clinic.Domain.Patients;

namespace Clinic.Application.UnitTests.Patients;

public sealed class PatientCommandHandlerTests
{
    [Fact]
    public async Task CreateNormalizesEgyptianPhonesBeforeCallingService()
    {
        FakePatientService service = new();
        CreatePatientCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientDetails> result = await handler.Handle(new CreatePatientCommand(
            ValidInput() with { PrimaryPhoneNumber = "+20 1012345678" }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("01012345678", service.CreatedInput?.PrimaryPhoneNumber);
    }

    [Fact]
    public async Task CreateRejectsUnauthenticatedCallerBeforeCallingService()
    {
        FakePatientService service = new();
        CreatePatientCommandHandler handler = new(new FakeCurrentUser(null), service);

        Result<PatientDetails> result = await handler.Handle(
            new CreatePatientCommand(ValidInput()), CancellationToken.None);

        Assert.Equal(PatientErrors.NotAuthenticated, result.Error);
        Assert.Null(service.CreatedInput);
    }

    [Fact]
    public async Task CreateRejectsBothBirthDateAndAge()
    {
        FakePatientService service = new();
        CreatePatientCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientDetails> result = await handler.Handle(new CreatePatientCommand(
            ValidInput() with { BirthDate = new DateOnly(1990, 1, 1) }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("patients.validation", result.Error.Code);
        Assert.Null(service.CreatedInput);
    }

    [Fact]
    public async Task UpdateRejectsInvalidRowVersion()
    {
        FakePatientService service = new();
        UpdatePatientCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientDetails> result = await handler.Handle(
            new UpdatePatientCommand(1, ValidInput(), "invalid"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("patients.validation", result.Error.Code);
    }

    [Fact]
    public async Task RestoreRequiresReasonAndValidRowVersion()
    {
        FakePatientService service = new();
        RestorePatientCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientDetails> result = await handler.Handle(
            new RestorePatientCommand(1, " ", "invalid"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("patients.validation", result.Error.Code);
        Assert.False(service.RestoreWasCalled);
    }

    private static PatientInput ValidInput() => new("مريض", "01012345678", null, null,
        30, PatientGender.Male, null, null, null, null, null);

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakePatientService : IPatientAdministrationService
    {
        public PatientInput? CreatedInput { get; private set; }
        public bool RestoreWasCalled { get; private set; }

        public Task<Result<PatientDetails>> CreatePatientAsync(long actorUserId,
            PatientInput input, CancellationToken cancellationToken)
        {
            CreatedInput = input;
            return Task.FromResult(Result.Success(Model()));
        }

        public Task<Result<PatientDetails>> UpdatePatientAsync(long actorUserId, long patientId,
            PatientInput input, byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Model()));

        public Task<Result> ArchivePatientAsync(long actorUserId, long patientId, string reason,
            byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<PatientDetails>> RestorePatientAsync(long actorUserId,
            long patientId, string reason, byte[] expectedRowVersion,
            CancellationToken cancellationToken)
        {
            RestoreWasCalled = true;
            return Task.FromResult(Result.Success(Model()));
        }

        public Task<Result<SensitiveNoteReceipt>> CreateNoteAsync(long actorUserId,
            long patientId, string noteText, PatientNoteVisibility visibility,
            CancellationToken cancellationToken) => Task.FromResult(Result.Success(
                new SensitiveNoteReceipt(1, DateTimeOffset.UtcNow)));

        public Task<Result> ArchiveNoteAsync(long actorUserId, long noteId, string reason,
            byte[] expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<TreatmentHistoryModel>> CreateTreatmentHistoryAsync(long actorUserId,
            long patientId, DateOnly eventDate, string description,
            CancellationToken cancellationToken) => Task.FromResult(Result.Success(
                new TreatmentHistoryModel(1, patientId, eventDate, description, actorUserId,
                    DateTimeOffset.UtcNow, false, Convert.ToBase64String(new byte[8]))));

        public Task<Result> ArchiveTreatmentHistoryAsync(long actorUserId,
            long treatmentHistoryId, string reason, byte[] expectedRowVersion,
            CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        private static PatientDetails Model() => new(1, "000001", "مريض", "01012345678",
            null, null, 30, new DateOnly(2026, 9, 6), 30, PatientGender.Male, null, null,
            null, null, null, false, DateTimeOffset.UtcNow, null,
            Convert.ToBase64String(new byte[8]));
    }
}
