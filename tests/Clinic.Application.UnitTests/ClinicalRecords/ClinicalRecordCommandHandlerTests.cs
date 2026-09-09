using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.ClinicalRecords;

namespace Clinic.Application.UnitTests.ClinicalRecords;

public sealed class ClinicalRecordCommandHandlerTests
{
    [Fact]
    public async Task CreateRejectsUnauthenticatedUserBeforeCallingService()
    {
        FakePrescriptionCommandService service = new();
        CreatePrescriptionDraftCommandHandler handler = new(
            new FakeCurrentUser(null), service);

        Result<PrescriptionModel> result = await handler.Handle(
            new CreatePrescriptionDraftCommand(1, ValidContent()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ContentRejectsBothReturnDateInputs()
    {
        FakePrescriptionCommandService service = new();
        CreatePrescriptionDraftCommandHandler handler = new(
            new FakeCurrentUser(1), service);
        PrescriptionContentInput invalid = ValidContent() with
        {
            ReturnAfterDays = 10
        };

        Result<PrescriptionModel> result = await handler.Handle(
            new CreatePrescriptionDraftCommand(1, invalid), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task FinalizeRequiresValidRowVersion()
    {
        FakePrescriptionCommandService service = new();
        FinalizePrescriptionCommandHandler handler = new(new FakeCurrentUser(1), service);

        Result<PrescriptionModel> result = await handler.Handle(
            new FinalizePrescriptionCommand(1, true, "invalid"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    private static PrescriptionContentInput ValidContent() => new(
        [new PrescriptionItemInput("دواء", 1, "قرص", 2, null, null, null, null)],
        new DateOnly(2026, 9, 20), null);

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakePrescriptionCommandService : IPrescriptionCommandService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<PrescriptionModel>> CreateDraftAsync(long actorUserId,
            long appointmentServiceId, PrescriptionContentInput content,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException();
        }

        public Task<Result<PrescriptionModel>> FinalizeAsync(long actorUserId,
            long prescriptionId, bool matchesDoctorPrescription, byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException();
        }

        public Task<Result<PrescriptionModel>> SaveDraftAsync(long actorUserId,
            long prescriptionId, PrescriptionContentInput content, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<PrescriptionModel>> CorrectAsync(long actorUserId,
            long prescriptionId, PrescriptionContentInput content, string reason,
            bool matchesDoctorPrescription, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<PrescriptionModel>> VoidAsync(long actorUserId,
            long prescriptionId, string reason, byte[] rowVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
